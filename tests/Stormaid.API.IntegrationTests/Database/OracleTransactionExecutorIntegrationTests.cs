
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stormaid.Application.Interfaces;
using Stormaid.Infrastructure;
using Stormaid.Infrastructure.Data;

namespace Stormaid.API.IntegrationTests.Database;

public sealed class OracleTransactionExecutorIntegrationTests
{
    private const string EventType = "test.transaction.v1";

    [OracleIntegrationFact]
    public async Task ExecuteAsync_ShouldCommit_WhenOperationSucceeds()
    {
        await using var provider = CreateProvider();

        var firstEventId =
            $"evt-test-tx-commit-a-{Guid.NewGuid():N}";

        var secondEventId =
            $"evt-test-tx-commit-b-{Guid.NewGuid():N}";

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var executor = scope.ServiceProvider
                    .GetRequiredService<ITransactionExecutor>();

                var outbox = scope.ServiceProvider
                    .GetRequiredService<IIntegrationEventOutbox>();

                var context = scope.ServiceProvider
                    .GetRequiredService<StormaidDbContext>();

                var result = await executor.ExecuteAsync(
                    async cancellationToken =>
                    {
                        await outbox.EnqueueAsync(
                            firstEventId,
                            EventType,
                            EventType,
                            "{\"step\":1}",
                            cancellationToken);

                        // Persiste sem confirmar a transação.
                        await context.SaveChangesAsync(
                            cancellationToken);

                        await outbox.EnqueueAsync(
                            secondEventId,
                            EventType,
                            EventType,
                            "{\"step\":2}",
                            cancellationToken);

                        return true;
                    });

                Assert.True(result);
            }

            // Novo contexto: verifica dados confirmados.
            await using (var scope = provider.CreateAsyncScope())
            {
                var context = scope.ServiceProvider
                    .GetRequiredService<StormaidDbContext>();

                Assert.True(
                    await context.OutboxMessages.AsNoTracking()
                        .AnyAsync(x => x.EventId == firstEventId));

                Assert.True(
                    await context.OutboxMessages.AsNoTracking()
                        .AnyAsync(x => x.EventId == secondEventId));
            }
        }
        finally
        {
            await CleanupAsync(
                provider,
                firstEventId,
                secondEventId);
        }
    }

    [OracleIntegrationFact]
    public async Task ExecuteAsync_ShouldRollback_AfterIntermediateSave()
    {
        await using var provider = CreateProvider();

        var firstEventId =
            $"evt-test-tx-rollback-a-{Guid.NewGuid():N}";

        var secondEventId =
            $"evt-test-tx-rollback-b-{Guid.NewGuid():N}";

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var executor = scope.ServiceProvider
                    .GetRequiredService<ITransactionExecutor>();

                var outbox = scope.ServiceProvider
                    .GetRequiredService<IIntegrationEventOutbox>();

                var context = scope.ServiceProvider
                    .GetRequiredService<StormaidDbContext>();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => executor.ExecuteAsync<int>(
                        async cancellationToken =>
                        {
                            await outbox.EnqueueAsync(
                                firstEventId,
                                EventType,
                                EventType,
                                "{\"step\":1}",
                                cancellationToken);

                            await context.SaveChangesAsync(
                                cancellationToken);

                            await outbox.EnqueueAsync(
                                secondEventId,
                                EventType,
                                EventType,
                                "{\"step\":2}",
                                cancellationToken);

                            await context.SaveChangesAsync(
                                cancellationToken);

                            throw new InvalidOperationException(
                                "Falha simulada após persistência.");
                        }));
            }

            // Novo contexto: nenhum evento deve existir.
            await using (var scope = provider.CreateAsyncScope())
            {
                var context = scope.ServiceProvider
                    .GetRequiredService<StormaidDbContext>();

                Assert.False(
                    await context.OutboxMessages.AsNoTracking()
                        .AnyAsync(x => x.EventId == firstEventId));

                Assert.False(
                    await context.OutboxMessages.AsNoTracking()
                        .AnyAsync(x => x.EventId == secondEventId));
            }
        }
        finally
        {
            await CleanupAsync(
                provider,
                firstEventId,
                secondEventId);
        }
    }


    private static ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: false)
            .Build();

        var connectionString =
            configuration.GetConnectionString("Oracle");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Configure ConnectionStrings:Oracle no user-secrets da API.");

        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }


    private static async Task CleanupAsync(
        ServiceProvider provider,
        params string[] eventIds)
    {
        await using var scope = provider.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<StormaidDbContext>();

        foreach (var eventId in eventIds)
        {
            var message = await context.OutboxMessages
                .FirstOrDefaultAsync(x => x.EventId == eventId);

            if (message is not null)
            {
                context.OutboxMessages.Remove(message);
            }
        }

        await context.SaveChangesAsync();
    }
}
