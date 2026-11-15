using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stormaid.Application.Interfaces;
using Stormaid.Infrastructure.Data;
using Stormaid.Infrastructure.Messaging;
using Stormaid.Infrastructure.Repositories;

namespace Stormaid.Infrastructure;

/// <summary>
/// Configura as dependências de infraestrutura
/// utilizadas pela aplicação Storm Aid.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra o contexto Oracle e as implementações
    /// dos repositórios e serviços de integração.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Oracle");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A connection string 'ConnectionStrings:Oracle' não foi configurada."
            );
        }

        services.AddDbContext<StormaidDbContext>(options =>
        {
            options.UseOracle(
                connectionString,
                oracleOptions =>
                    oracleOptions.UseOracleSQLCompatibility(
                        OracleSQLCompatibility.DatabaseVersion19
                    )
            );
        });

        services.AddScoped<
            IIntegrationEventOutbox,
            IntegrationEventOutbox>();
        
        services.AddScoped<
            IOutboxEventPublisher,
            RabbitMqOutboxPublisher>();

        services.AddScoped<
            IRescueUnitRepository,
            RescueUnitRepository>();
        
        services.AddScoped<
            IOutboxMessageRepository,
            OutboxMessageRepository>();
        
        services.AddScoped<
            IRegulationDecisionRepository,
            RegulationDecisionRepository>();

        services.AddScoped<
            IIncidentReferenceRepository,
            IncidentReferenceRepository>();

        return services;
    }
}