using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stormaid.API.Messaging;

namespace Stormaid.API.IntegrationTests.Factories;

public sealed class StormaidWebApplicationFactory
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RabbitMQ:Enabled"] = "false"
                }
            );
        });

        builder.ConfigureTestServices(services =>
        {
            var registration = services.FirstOrDefault(
                descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType ==
                    typeof(IncidentCreatedConsumer)
            );

            if (registration is not null)
            {
                services.Remove(registration);
            }
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Oracle"] =
                        "User Id=test;Password=test;Data Source=localhost:1521/NOT_USED",

                    ["Jwt:Secret"] = new string('x', 32)
                }
            );
        });

        return base.CreateHost(builder);
    }
}