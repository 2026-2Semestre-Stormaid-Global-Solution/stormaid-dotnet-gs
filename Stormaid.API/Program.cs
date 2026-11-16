using System.Text.Json.Serialization;
using Stormaid.API.Configurations;
using Stormaid.API.Controllers;
using Stormaid.Application.Services;
using Stormaid.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Stormaid.API.Health;
using Stormaid.Infrastructure.Messaging;
using Stormaid.API.Messaging;
using Serilog;
using OpenTelemetry.Metrics;

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

builder.Services.AddSerilog((services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

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

builder.Services.AddStormaidObservability(builder.Configuration);

builder.Services.AddScoped<RescueUnitQueryService>();
builder.Services.AddScoped<RescueUnitCommandService>();
builder.Services.AddScoped<IncidentCreatedEventService>();
builder.Services.AddScoped<IncidentReferenceQueryService>();
builder.Services.AddScoped<IncidentReferenceCommandService>();
builder.Services.AddScoped<RegulationDecisionCommandService>();
builder.Services.AddScoped<IncidentDispatchCommandService>();

var app = builder.Build();

var openApiEnabled =
    app.Environment.IsDevelopment() ||
    app.Configuration.GetValue<bool>("OpenApi:Enabled");

if (openApiEnabled)
{
    app.MapOpenApi();

    app.UseSwaggerUi(options =>
    {
        options.DocumentTitle = "Storm Aid | API de Regulação";
        options.SwaggerRoutes.Add(new("Storm Aid API v1", "/openapi/v1.json"));
    });
}

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000} ms";
});

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapPrometheusScrapingEndpoint()
        .DisableHttpMetrics()
        .AllowAnonymous();
}


app.MapGet("/", () => Results.Ok(new
    {
        name = "Storm Aid API",
        version = "v1",
        description = "API institucional de regulação de emergências, gestão de unidades de resgate e acompanhamento de operações.",

        documentation = new
        {
            swagger = openApiEnabled ? "/swagger/index.html" : null,
            openApi = openApiEnabled ? "/openapi/v1.json" : null
        },

        health = new
        {
            general = "/health",
            live = "/health/live",
            ready = "/health/ready"
        },

        authentication = new
        {
            type = "Bearer JWT",
            provider = "Storm Aid Java API",
            roles = new[] { "CITIZEN", "REGULATOR", "RESPONDER" }
        },

        routes = new[]
        {
            new
            {
                area = "Acompanhamento do cidadão",
                path = "/api/v1/citizens/incidents/{javaIncidentId}/tracking",
                access = "CITIZEN"
            },
            new
            {
                area = "Regulação de ocorrências",
                path = "/api/v1/regulation/incidents",
                access = "REGULATOR"
            },
            new
            {
                area = "Unidades de resgate",
                path = "/api/v1/rescue-units",
                access = "REGULATOR / RESPONDER, conforme operação"
            },
            new
            {
                area = "Despachos operacionais",
                path = "/api/v1/dispatches",
                access = "REGULATOR / RESPONDER, conforme operação"
            }
        }
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