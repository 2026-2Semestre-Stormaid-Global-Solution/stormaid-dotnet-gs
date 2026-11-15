using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stormaid.Application.Services;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;
using Stormaid.Infrastructure;
using Stormaid.Infrastructure.Data;
using Stormaid.Infrastructure.Messaging;
using Stormaid.Infrastructure.Repositories;

namespace Stormaid.API.IntegrationTests.Database;

/// <summary>
/// Valida a concorrência otimista de ocorrências
/// utilizando contextos e conexões independentes
/// contra um banco Oracle de desenvolvimento.
/// </summary>
public sealed class OracleConcurrencyIntegrationTests
{
    private const long TestIncidentId = 900003;

    private const long FirstRegulatorId = 1;

    private const long SecondRegulatorId = 2;

    private const string ExpectedEventId =
        "evt-regulation-900003-1";

    /// <summary>
    /// Garante que uma atualização com VERSION
    /// desatualizada seja rejeitada pelo EF Core,
    /// preservando a primeira análise registrada.
    /// </summary>
    [OracleIntegrationFact]
    public async Task BeginReview_ShouldRejectStaleVersion_AndPreserveFirstReview()
    {
        // 1 - Configurar o Oracle de desenvolvimento.

        var connectionString =
            Environment.GetEnvironmentVariable(
                "STORMAID_TEST_ORACLE_CONNECTION_STRING"
            );

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Configure a conexão Oracle de desenvolvimento."
        );

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Oracle"] = connectionString
                }
            )
            .Build();

        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        await using var provider =
            services.BuildServiceProvider();

        var created = false;

        try
        {
            // 2 - Criar um registro de teste.
            //
            // Diferentemente do rollback anterior,
            // a ocorrência precisa ser confirmada
            // para que duas conexões possam lê-la.

            await using (var setupScope =
                provider.CreateAsyncScope())
            {
                var setupContext = setupScope.ServiceProvider
                    .GetRequiredService<StormaidDbContext>();

                Assert.False(
                    await setupContext.IncidentReferences
                        .AsNoTracking()
                        .AnyAsync(x =>
                            x.JavaIncidentId == TestIncidentId),
                    "A ocorrência de teste já existe."
                );

                Assert.False(
                    await setupContext.OutboxMessages
                        .AsNoTracking()
                        .AnyAsync(x =>
                            x.EventId == ExpectedEventId),
                    "O evento de teste já existe."
                );

                var incident = new IncidentReference(
                    javaIncidentId: TestIncidentId,
                    requesterUserId: 1,
                    recipientType: RecipientType.SELF,
                    channelType: ChannelType.TEXT_INPUT,
                    emergencyCategory:
                        EmergencyCategory.CLIMATE_DISASTER,
                    latitude: -23.5505m,
                    longitude: -46.6333m,
                    addressText:
                        "Teste de concorrencia - Storm Aid",
                    javaCreatedAt: null,
                    utcNow: DateTime.UtcNow.AddMinutes(-10)
                );

                await setupContext.IncidentReferences
                    .AddAsync(incident);

                await setupContext.SaveChangesAsync();

                created = true;
            }

            // 3 - Criar dois escopos independentes.
            //
            // Cada escopo possui seu próprio
            // StormaidDbContext e conexão Oracle.

            await using var firstScope =
                provider.CreateAsyncScope();

            await using var secondScope =
                provider.CreateAsyncScope();

            var firstContext = firstScope.ServiceProvider
                .GetRequiredService<StormaidDbContext>();

            var secondContext = secondScope.ServiceProvider
                .GetRequiredService<StormaidDbContext>();

            Assert.NotSame(firstContext, secondContext);

            // 4 - Ambos carregam a ocorrência
            // enquanto ela está PENDING_REVIEW.

            var firstIncident =
                await firstContext.IncidentReferences
                    .AsTracking()
                    .SingleAsync(x =>
                        x.JavaIncidentId == TestIncidentId);

            var secondIncident =
                await secondContext.IncidentReferences
                    .AsTracking()
                    .SingleAsync(x =>
                        x.JavaIncidentId == TestIncidentId);

            Assert.Equal(
                RegulationStatus.PENDING_REVIEW,
                firstIncident.RegulationStatus
            );

            Assert.Equal(
                RegulationStatus.PENDING_REVIEW,
                secondIncident.RegulationStatus
            );

            Assert.Equal(
                firstIncident.Version,
                secondIncident.Version
            );

            Assert.Equal(0L, firstIncident.Version);

            // 5 - O primeiro regulador utiliza
            // o serviço real da aplicação.
            //
            // A operação altera a ocorrência
            // e registra um evento na Outbox.

            var firstService =
                new IncidentReferenceCommandService(
                    new IncidentReferenceRepository(
                        firstContext),
                    new IntegrationEventOutbox(
                        firstContext)
                );

            var firstResult =
                await firstService.BeginReviewAsync(
                    TestIncidentId,
                    FirstRegulatorId
                );

            Assert.True(firstResult.Changed);

            Assert.Equal(
                RegulationStatus.UNDER_REVIEW,
                firstResult.RegulationStatus
            );

            // 6 - O segundo regulador continua
            // com a entidade desatualizada em memória.
            //
            // Simulamos a tentativa de salvar
            // a alteração com a VERSION antiga.

            var secondNow = DateTime.UtcNow;

            secondIncident.BeginReview(
                SecondRegulatorId,
                secondNow
            );

            secondIncident.AdvancePublicStatusVersion(
                secondNow
            );

            // 7 - A atualização deve ser rejeitada
            // pela verificação do token VERSION.

            await Assert.ThrowsAsync<
                DbUpdateConcurrencyException>(
                () => secondContext.SaveChangesAsync()
            );

            // 8 - Verificar o resultado confirmado
            // através de um terceiro contexto.

            await using var verificationScope =
                provider.CreateAsyncScope();

            var verificationContext =
                verificationScope.ServiceProvider
                    .GetRequiredService<StormaidDbContext>();

            var storedIncident =
                await verificationContext.IncidentReferences
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.JavaIncidentId == TestIncidentId);

            Assert.Equal(
                RegulationStatus.UNDER_REVIEW,
                storedIncident.RegulationStatus
            );

            Assert.Equal(
                FirstRegulatorId,
                storedIncident.ReviewedByRegulatorUserId
            );

            Assert.Equal(
                1L,
                storedIncident.PublicStatusVersion
            );

            Assert.Equal(2L, storedIncident.Version);

            Assert.NotNull(
                storedIncident.ReviewStartedAt
            );

            // 9 - A primeira operação deve
            // ter gerado exatamente um evento.

            var eventCount =
                await verificationContext.OutboxMessages
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.EventId == ExpectedEventId);

            Assert.Equal(1, eventCount);
        }
        finally
        {
            // 10 - Limpar os registros que este teste
            // conseguiu criar.
            //
            // Executado também quando uma asserção
            // falhar durante o teste.

            if (created)
            {
                await using var cleanupScope =
                    provider.CreateAsyncScope();

                var cleanupContext =
                    cleanupScope.ServiceProvider
                        .GetRequiredService<StormaidDbContext>();

                await using var cleanupTransaction =
                    await cleanupContext.Database
                        .BeginTransactionAsync();

                await cleanupContext.Database
                    .ExecuteSqlInterpolatedAsync(
                        $"DELETE FROM TB_STORMAID_OUTBOX_MESSAGE WHERE EVENT_ID = {ExpectedEventId}"
                    );

                await cleanupContext.Database
                    .ExecuteSqlInterpolatedAsync(
                        $"DELETE FROM TB_STORMAID_INCIDENT_REFERENCE WHERE JAVA_INCIDENT_ID = {TestIncidentId}"
                    );

                await cleanupTransaction.CommitAsync();
            }
        }
    }
}
