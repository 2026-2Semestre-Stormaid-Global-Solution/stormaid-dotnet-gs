using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stormaid.Infrastructure.Data;

namespace Stormaid.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Oracle");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("A connection string 'ConnectionStrings:Oracle' não foi configurada.");
        }

        services.AddDbContext<StormaidDbContext>(options =>
        {
            options.UseOracle(connectionString);
        });

        return services;
    }
}