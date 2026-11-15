using System.Text.Json;
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

public sealed class OracleRegulationDecisionIntegrationTests
{
    private const long TestIncidentId = 900004;
    private const long RegulatorUserId = 1;
    private const string ExpectedEventId = "evt-regulation-900004-2";
    private const string EventType = "incident.status.changed.v1";
    private const string Justification = "Ocorrência confirmada com necessidade de atendimento do Corpo de Bombeiros.";


    [OracleIntegrationFact]
    public async Task RegisterDecision_ShouldRollback_WhenOutboxInsertFails()
    {
        const long testIncidentId = 900005;
        const long regulatorUserId = 1;
        const string duplicateEventId = "evt-regulation-900005-2";

        var connectionString = Environment.GetEnvironmentVariable(
            "STORMAID_TEST_ORACLE_CONNECTION_STRING");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Configure a conexão Oracle de desenvolvimento.");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Oracle"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        await using var provider = services.BuildServiceProvider();

        var incidentReferenceId = 0L;

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<StormaidDbContext>();

            Assert.False(await context.IncidentReferences.AsNoTracking()
                .AnyAsync(x => x.JavaIncidentId == testIncidentId));

            Assert.False(await context.OutboxMessages.AsNoTracking()
                .AnyAsync(x => x.EventId == duplicateEventId));

            await using var transaction = await context.Database.BeginTransactionAsync();

            var utcNow = DateTime.UtcNow.AddMinutes(-10);
            var reviewTime = utcNow.AddMinutes(1);

            var incident = new IncidentReference(
                javaIncidentId: testIncidentId,
                requesterUserId: 1,
                recipientType: RecipientType.SELF,
                channelType: ChannelType.TEXT_INPUT,
                emergencyCategory: EmergencyCategory.CLIMATE_DISASTER,
                latitude: -23.5505m,
                longitude: -46.6333m,
                addressText: "Teste de rollback da decisão - Storm Aid",
                javaCreatedAt: null,
                utcNow: utcNow);

            incident.BeginReview(regulatorUserId, reviewTime);
            incident.AdvancePublicStatusVersion(reviewTime);

            var existingEvent = new OutboxMessage(
                eventId: duplicateEventId,
                eventType: "incident.status.changed.v1",
                routingKey: "incident.status.changed.v1",
                payload: "{\"scenario\":\"decision-rollback-test\"}",
                utcNow: DateTime.UtcNow);

            await context.IncidentReferences.AddAsync(incident);
            await context.OutboxMessages.AddAsync(existingEvent);
            await context.SaveChangesAsync();

            incidentReferenceId = incident.Id;

            context.ChangeTracker.Clear();

            var service = new RegulationDecisionCommandService(
                scope.ServiceProvider.GetRequiredService<IIncidentReferenceRepository>(),
                scope.ServiceProvider.GetRequiredService<IRegulationDecisionRepository>(),
                scope.ServiceProvider.GetRequiredService<IIntegrationEventOutbox>());

            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => service.RegisterAsync(
                    javaIncidentId: testIncidentId,
                    regulatorUserId: regulatorUserId,
                    decisionType: RegulationDecisionType.DISPATCH_APPROVED,
                    institutionType: ResponderInstitutionType.FIRE_DEPARTMENT,
                    justification: "Ocorrência confirmada para atendimento do Corpo de Bombeiros."));

            Assert.Contains("ORA-00001", exception.ToString());

            context.ChangeTracker.Clear();

            var storedIncident = await context.IncidentReferences
                .AsNoTracking()
                .SingleAsync(x => x.JavaIncidentId == testIncidentId);

            Assert.Equal(RegulationStatus.UNDER_REVIEW, storedIncident.RegulationStatus);
            Assert.Equal(1L, storedIncident.PublicStatusVersion);
            Assert.Equal(2L, storedIncident.Version);
            Assert.Equal(regulatorUserId, storedIncident.ReviewedByRegulatorUserId);

            var decisionCount = await context.RegulationDecisions
                .AsNoTracking()
                .CountAsync(x => x.IncidentReferenceId == incidentReferenceId);

            Assert.Equal(0, decisionCount);

            var eventCount = await context.OutboxMessages
                .AsNoTracking()
                .CountAsync(x => x.EventId == duplicateEventId);

            Assert.Equal(1, eventCount);

            await transaction.RollbackAsync();
        }

        await using var verificationScope = provider.CreateAsyncScope();

        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<StormaidDbContext>();

        Assert.False(await verificationContext.IncidentReferences
            .AsNoTracking()
            .AnyAsync(x => x.JavaIncidentId == testIncidentId));

        Assert.False(await verificationContext.RegulationDecisions
            .AsNoTracking()
            .AnyAsync(x => x.IncidentReferenceId == incidentReferenceId));

        Assert.False(await verificationContext.OutboxMessages
            .AsNoTracking()
            .AnyAsync(x => x.EventId == duplicateEventId));
    }

    [OracleIntegrationFact]
    public async Task RegisterDecision_ShouldPersistDecisionAndOutbox_WhenDispatchIsApproved()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "STORMAID_TEST_ORACLE_CONNECTION_STRING");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Configure a conexão Oracle de desenvolvimento.");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Oracle"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        await using var provider = services.BuildServiceProvider();

        var created = false;
        var incidentReferenceId = 0L;

        try
        {
            await using (var setupScope = provider.CreateAsyncScope())
            {
                var setupContext = setupScope.ServiceProvider
                    .GetRequiredService<StormaidDbContext>();

                Assert.False(
                    await setupContext.IncidentReferences.AsNoTracking()
                        .AnyAsync(x => x.JavaIncidentId == TestIncidentId),
                    "A ocorrência de teste já existe no Oracle.");

                Assert.False(
                    await setupContext.OutboxMessages.AsNoTracking()
                        .AnyAsync(x => x.EventId == ExpectedEventId),
                    "O identificador de evento reservado já existe.");

                var utcNow = DateTime.UtcNow.AddMinutes(-10);
                var reviewTime = utcNow.AddMinutes(1);

                var incident = new IncidentReference(
                    javaIncidentId: TestIncidentId,
                    requesterUserId: 1,
                    recipientType: RecipientType.SELF,
                    channelType: ChannelType.TEXT_INPUT,
                    emergencyCategory: EmergencyCategory.CLIMATE_DISASTER,
                    latitude: -23.5505m,
                    longitude: -46.6333m,
                    addressText: "Teste de decisão regulatória - Storm Aid",
                    javaCreatedAt: null,
                    utcNow: utcNow);

                incident.BeginReview(RegulatorUserId, reviewTime);
                incident.AdvancePublicStatusVersion(reviewTime);

                await setupContext.IncidentReferences.AddAsync(incident);
                await setupContext.SaveChangesAsync();

                created = true;
                incidentReferenceId = incident.Id;

                Assert.True(incidentReferenceId > 0);
            }

            await using (var executionScope = provider.CreateAsyncScope())
            {
                var service = new RegulationDecisionCommandService(
                    executionScope.ServiceProvider
                        .GetRequiredService<IIncidentReferenceRepository>(),
                    executionScope.ServiceProvider
                        .GetRequiredService<IRegulationDecisionRepository>(),
                    executionScope.ServiceProvider
                        .GetRequiredService<IIntegrationEventOutbox>());

                var result = await service.RegisterAsync(
                    javaIncidentId: TestIncidentId,
                    regulatorUserId: RegulatorUserId,
                    decisionType: RegulationDecisionType.DISPATCH_APPROVED,
                    institutionType: ResponderInstitutionType.FIRE_DEPARTMENT,
                    justification: Justification);

                Assert.True(result.Changed);
                Assert.True(result.DecisionId > 0);
                Assert.Equal(TestIncidentId, result.JavaIncidentId);
                Assert.Equal(RegulatorUserId, result.RegulatorUserId);
                Assert.Equal(RegulationDecisionType.DISPATCH_APPROVED, result.DecisionType);
                Assert.Equal(ResponderInstitutionType.FIRE_DEPARTMENT, result.InstitutionType);
                Assert.Equal(RegulationStatus.AWAITING_DISPATCH, result.RegulationStatus);
                Assert.Equal(2L, result.PublicStatusVersion);
            }

            await using (var verificationScope = provider.CreateAsyncScope())
            {
                var context = verificationScope.ServiceProvider
                    .GetRequiredService<StormaidDbContext>();

                var storedIncident = await context.IncidentReferences
                    .AsNoTracking()
                    .SingleAsync(x => x.JavaIncidentId == TestIncidentId);

                Assert.Equal(incidentReferenceId, storedIncident.Id);
                Assert.Equal(RegulationStatus.AWAITING_DISPATCH, storedIncident.RegulationStatus);
                Assert.Equal(RegulatorUserId, storedIncident.ReviewedByRegulatorUserId);
                Assert.Equal(2L, storedIncident.PublicStatusVersion);
                Assert.Equal(4L, storedIncident.Version);

                var storedDecision = await context.RegulationDecisions
                    .AsNoTracking()
                    .SingleAsync(x => x.IncidentReferenceId == incidentReferenceId);

                Assert.True(storedDecision.Id > 0);
                Assert.Equal(incidentReferenceId, storedDecision.IncidentReferenceId);
                Assert.Equal(RegulatorUserId, storedDecision.RegulatorUserId);
                Assert.Equal(RegulationDecisionType.DISPATCH_APPROVED, storedDecision.DecisionType);
                Assert.Equal(ResponderInstitutionType.FIRE_DEPARTMENT, storedDecision.InstitutionType);
                Assert.Equal(Justification, storedDecision.Justification);

                var storedEvent = await context.OutboxMessages
                    .AsNoTracking()
                    .SingleAsync(x => x.EventId == ExpectedEventId);

                Assert.Equal(ExpectedEventId, storedEvent.EventId);
                Assert.Equal(EventType, storedEvent.EventType);
                Assert.Equal(EventType, storedEvent.RoutingKey);

                using var payload = JsonDocument.Parse(storedEvent.Payload);

                var root = payload.RootElement;

                Assert.Equal(ExpectedEventId, root.GetProperty("eventId").GetString());
                Assert.Equal(EventType, root.GetProperty("eventType").GetString());
                Assert.Equal(TestIncidentId, root.GetProperty("incidentId").GetInt64());
                Assert.Equal("AWAITING_DISPATCH", root.GetProperty("status").GetString());
                Assert.Equal(2L, root.GetProperty("version").GetInt64());
            }
        }
        finally
        {
            if (created)
            {
                await using var cleanupScope = provider.CreateAsyncScope();

                var context = cleanupScope.ServiceProvider
                    .GetRequiredService<StormaidDbContext>();

                await using var transaction = await context.Database.BeginTransactionAsync();

                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM TB_STORMAID_OUTBOX_MESSAGE WHERE EVENT_ID = {ExpectedEventId}");

                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM TB_STORMAID_REGULATION_DECISION WHERE INCIDENT_REFERENCE_ID = {incidentReferenceId}");

                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM TB_STORMAID_INCIDENT_REFERENCE WHERE JAVA_INCIDENT_ID = {TestIncidentId}");

                await transaction.CommitAsync();
            }
        }

        await using var finalScope = provider.CreateAsyncScope();

        var finalContext = finalScope.ServiceProvider
            .GetRequiredService<StormaidDbContext>();

        Assert.False(await finalContext.IncidentReferences.AsNoTracking()
            .AnyAsync(x => x.JavaIncidentId == TestIncidentId));

        Assert.False(await finalContext.OutboxMessages.AsNoTracking()
            .AnyAsync(x => x.EventId == ExpectedEventId));

        Assert.False(await finalContext.RegulationDecisions.AsNoTracking()
            .AnyAsync(x => x.IncidentReferenceId == incidentReferenceId));
    }
}
