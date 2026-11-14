using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Stormaid.API.IntegrationTests.Factories;

/// <summary>
/// Configura o ambiente utilizado pelos testes
/// de integração da API Storm Aid.
/// </summary>
public sealed class StormaidWebApplicationFactory
    : WebApplicationFactory<Program>
{
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
                });
        });

        return base.CreateHost(builder);
    }
}