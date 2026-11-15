
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stormaid.Application.Interfaces;
using Stormaid.Application.Services;
using Stormaid.Domain.Entities;
using Stormaid.Domain.Enums;
using Stormaid.Infrastructure;
using Stormaid.Infrastructure.Data;
using Stormaid.Infrastructure.Data.Entities;

namespace Stormaid.API.IntegrationTests.Database;

/// <summary>
/// Habilita testes reais de Oracle somente quando
/// a execução for solicitada explicitamente.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OracleIntegrationFactAttribute : FactAttribute
{
    public OracleIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(
                "STORMAID_RUN_ORACLE_TESTS") != "1")
        {
            Skip =
                "Teste Oracle desativado. " +
                "Defina STORMAID_RUN_ORACLE_TESTS=1.";
        }
    }
}

/// <summary>
/// Testes de integração real da persistência
/// transacional entre ocorrência e Outbox no Oracle.
/// </summary>
public sealed class OracleRollbackIntegrationTests
{
    private const long TestIncidentId = 900002;
    private const long RegulatorUserId = 1;

    private const string DuplicateEventId =
        "evt-regulation-900002-1";

    /// <summary>
    /// Verifica que uma violação de unicidade na
    /// Outbox não confirma a alteração da ocorrência.
    ///
    /// Os registros de preparação são criados dentro
    /// de uma transação descartável, sem COMMIT.
    /// </summary>
    [OracleIntegrationFact]
    public async Task BeginReview_ShouldRollback_WhenOutboxInsertFails()
    {
        // 1 - Obter a conexão de teste.

        var connectionString =
            Environment.GetEnvironmentVariable(
                "STORMAID_TEST_ORACLE_CONNECTION_STRING"
            );

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Configure a connection string do Oracle " +
            "de desenvolvimento antes de executar o teste."
        );

        // 2 - Utilizar os registros reais de
        // infraestrutura da aplicação.

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

        await using var scope =
            provider.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<StormaidDbContext>();

        // 3 - Garantir que o teste não utilize
        // registros previamente existentes.

        Assert.False(
            await context.IncidentReferences
                .AsNoTracking()
                .AnyAsync(x =>
                    x.JavaIncidentId == TestIncidentId),
            "A ocorrência 900002 já existe neste banco."
        );

        Assert.False(
            await context.OutboxMessages
                .AsNoTracking()
                .AnyAsync(x =>
                    x.EventId == DuplicateEventId),
            "O identificador de evento reservado " +
            "para o teste já existe."
        );

        // 4 - Abrir uma transação exclusiva
        // para preparar o cenário de teste.

        await using var transaction =
            await context.Database.BeginTransactionAsync();

        // 5 - Criar a ocorrência em PENDING_REVIEW.

        var incident = new IncidentReference(
            javaIncidentId: TestIncidentId,
            requesterUserId: 1,
            recipientType: RecipientType.SELF,
            channelType: ChannelType.TEXT_INPUT,
            emergencyCategory:
                EmergencyCategory.CLIMATE_DISASTER,
            latitude: -23.5505m,
            longitude: -46.6333m,
            addressText: "Teste de rollback - Storm Aid",
            javaCreatedAt: null,
            utcNow: DateTime.UtcNow.AddMinutes(-10)
        );

        // 6 - Criar previamente uma mensagem com
        // o mesmo EVENT_ID que o serviço utilizará.
        //
        // O objetivo é provocar ORA-00001 quando
        // o serviço tentar inserir o segundo evento.

        var existingEvent = new OutboxMessage(
            eventId: DuplicateEventId,
            eventType: "incident.status.changed.v1",
            routingKey: "incident.status.changed.v1",
            payload: "{\"scenario\":\"rollback-test\"}",
            utcNow: DateTime.UtcNow
        );

        await context.IncidentReferences.AddAsync(
            incident
        );

        await context.OutboxMessages.AddAsync(
            existingEvent
        );

        await context.SaveChangesAsync();

        // 7 - Limpar o rastreamento para simular
        // uma nova operação sobre a ocorrência.

        context.ChangeTracker.Clear();

        var repository = scope.ServiceProvider
            .GetRequiredService<IIncidentReferenceRepository>();

        var outbox = scope.ServiceProvider
            .GetRequiredService<IIntegrationEventOutbox>();

        var service = new IncidentReferenceCommandService(
            repository,
            outbox
        );

        // 8 - Executar o serviço real.
        //
        // O SaveChanges tentará atualizar a ocorrência
        // e inserir um EVENT_ID que já existe.

        var exception =
            await Assert.ThrowsAsync<DbUpdateException>(
                () => service.BeginReviewAsync(
                    TestIncidentId,
                    RegulatorUserId
                )
            );

        // 9 - Confirmar que a falha foi causada
        // pela restrição UNIQUE do Oracle.

        Assert.Contains(
            "ORA-00001",
            exception.ToString()
        );

        // 10 - Descartar os estados modificados
        // somente em memória pelo EF Core.

        context.ChangeTracker.Clear();

        // 11 - Consultar novamente os valores físicos
        // dentro da mesma transação de teste.

        var storedIncident = await context.IncidentReferences
            .AsNoTracking()
            .SingleAsync(x =>
                x.JavaIncidentId == TestIncidentId
            );

        Assert.Equal(
            RegulationStatus.PENDING_REVIEW,
            storedIncident.RegulationStatus
        );

        Assert.Equal(
            0L,
            storedIncident.PublicStatusVersion
        );

        Assert.Equal(
            0L,
            storedIncident.Version
        );

        Assert.Null(
            storedIncident.ReviewedByRegulatorUserId
        );

        Assert.Null(
            storedIncident.ReviewStartedAt
        );

        // 12 - Confirmar que somente a mensagem
        // previamente criada continua na transação.

        var eventCount = await context.OutboxMessages
            .AsNoTracking()
            .CountAsync(x =>
                x.EventId == DuplicateEventId
            );

        Assert.Equal(1, eventCount);

        // 13 - Desfazer também os registros criados
        // para preparar o teste.
        //
        // Não existe COMMIT nesta transação.

        await transaction.RollbackAsync();

        // 14 - Confirmar com outro DbContext que
        // nenhum dos registros de teste permaneceu.

        await using var verificationScope =
            provider.CreateAsyncScope();

        var verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<StormaidDbContext>();

        Assert.False(
            await verificationContext.IncidentReferences
                .AsNoTracking()
                .AnyAsync(x =>
                    x.JavaIncidentId == TestIncidentId)
        );

        Assert.False(
            await verificationContext.OutboxMessages
                .AsNoTracking()
                .AnyAsync(x =>
                    x.EventId == DuplicateEventId)
        );
    }
}
