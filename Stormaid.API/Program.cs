using System.Text.Json.Serialization;
using Stormaid.API.Configurations;
using Stormaid.API.Controllers;
using Stormaid.Application.Services;
using Stormaid.Infrastructure;

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

builder.Services.AddScoped<RescueUnitQueryService>();

builder.Services.AddStormaidJwt(builder.Configuration);

builder.Services.AddStormaidRateLimiting();

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

app.MapRescueUnitRoutes();

app.Run();