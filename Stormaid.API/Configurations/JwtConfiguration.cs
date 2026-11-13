
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Stormaid.API.Configurations;

/// <summary>
/// Configuração da autenticação JWT e das políticas
/// de autorização institucional do Storm Aid.
/// </summary>
public static class JwtConfiguration
{
    /// <summary>
    /// Registra a validação dos tokens JWT emitidos pela
    /// API Java e as políticas de acesso da API .NET.
    /// </summary>
    /// <param name="services">
    /// Coleção de serviços da aplicação.
    /// </param>
    /// <param name="configuration">
    /// Configurações da aplicação.
    /// </param>
    /// <returns>
    /// Coleção de serviços com a autenticação e
    /// autorização configuradas.
    /// </returns>
    public static IServiceCollection AddStormaidJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"];

        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Secret deve conter uma chave compartilhada " +
                "com o Java de pelo menos 32 bytes."
            );
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = "stormaid-api",

                        ValidateAudience = true,
                        ValidAudience = "stormaid-services",

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),

                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        RequireSignedTokens = true,

                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                        ClockSkew = TimeSpan.FromSeconds(30),

                        NameClaimType = "sub",
                        RoleClaimType = "roles"
                    };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("InstitutionalAccess", policy =>
            {
                policy.RequireAuthenticatedUser();

                policy.RequireRole("REGULATOR", "RESPONDER");
            });
        });

        return services;
    }
}
