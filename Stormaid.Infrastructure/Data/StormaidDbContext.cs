using Microsoft.EntityFrameworkCore;
using Stormaid.Domain.Entities;
using Stormaid.Infrastructure.Data.Entities;

namespace Stormaid.Infrastructure.Data;

/// <summary>
/// Contexto de persistência Oracle das entidades
/// operacionais e mensagens de integração do Storm Aid.
/// </summary>
public class StormaidDbContext : DbContext
{
    public StormaidDbContext(
        DbContextOptions<StormaidDbContext> options)
        : base(options)
    {
    }

    public DbSet<RescueUnit> RescueUnits =>
        Set<RescueUnit>();

    public DbSet<UnitAssignment> UnitAssignments =>
        Set<UnitAssignment>();

    public DbSet<IncidentReference> IncidentReferences =>
        Set<IncidentReference>();

    public DbSet<RegulationDecision> RegulationDecisions =>
        Set<RegulationDecision>();

    public DbSet<IncidentDispatch> IncidentDispatches =>
        Set<IncidentDispatch>();

    public DbSet<DispatchStatusHistory> DispatchStatusHistories =>
        Set<DispatchStatusHistory>();

    public DbSet<SupportRequest> SupportRequests =>
        Set<SupportRequest>();

    public DbSet<IdempotencyRecord> IdempotencyRecords =>
        Set<IdempotencyRecord>();

    public DbSet<ProcessedIntegrationEvent> ProcessedIntegrationEvents =>
        Set<ProcessedIntegrationEvent>();

    /// <summary>
    /// Mensagens de saída persistidas para publicação
    /// confiável na integração com a API Java.
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages =>
        Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(StormaidDbContext).Assembly
        );
    }
}