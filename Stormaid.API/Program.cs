using System.Text.Json.Serialization;
using Stormaid.API.Configurations;
using Stormaid.API.Controllers;
using Stormaid.Application.Services;
using Stormaid.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Stormaid.API.Health;
using Stormaid.Infrastructure.Messaging;
using Stormaid.API.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStormaidOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false
        )
    );
});

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(
        RabbitMqOptions.SectionName
    )
);

if (builder.Configuration.GetValue<bool>("RabbitMQ:Enabled"))
{
    builder.Services.AddHostedService<IncidentCreatedConsumer>();
}

if (builder.Configuration.GetValue<bool>("OutboxPublisher:Enabled"))
{
    builder.Services.AddHostedService<
        OutboxPublisherBackgroundService>();
}

builder.Services.AddStormaidJwt(builder.Configuration);

builder.Services.AddStormaidRateLimiting();

builder.Services.AddStormaidHealthChecks();

builder.Services.AddScoped<RescueUnitQueryService>();
builder.Services.AddScoped<RescueUnitCommandService>();
builder.Services.AddScoped<IncidentCreatedEventService>();
builder.Services.AddScoped<IncidentReferenceQueryService>();
builder.Services.AddScoped<IncidentReferenceCommandService>();
builder.Services.AddScoped<RegulationDecisionCommandService>();
builder.Services.AddScoped<IncidentDispatchCommandService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUi(options =>
    {
        options.DocumentPath = "/openapi/v1.json";
    });
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new
    {
        name = "Storm Aid API",
        version = "v1",
        description = "API de regulação e operações de resgate."
    }))
    .AllowAnonymous()
    .ExcludeFromDescription();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});

app.MapRegulationRoutes();

app.MapRescueUnitRoutes();

app.MapCitizenIncidentTrackingRoutes();

app.MapIncidentDispatchRoutes();

app.Run();

/// <summary>
/// Ponto de entrada da API Storm Aid.
/// Permite a inicialização da aplicação nos testes de integração.
/// </summary>
public partial class Program
{
}