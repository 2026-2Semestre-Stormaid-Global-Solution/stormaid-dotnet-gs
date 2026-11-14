using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Stormaid.API.IntegrationTests.Helpers;

/// <summary>
/// Gera tokens JWT para testes de autenticação
/// e autorização da API Storm Aid.
/// </summary>
public static class JwtTokenFactory
{
    public const string TestSecret = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

    public static string CreateToken(
        string secret,
        string? role = "CITIZEN",
        DateTime? expiresAtUtc = null,
        string issuer = "stormaid-api",
        string audience = "stormaid-services",
        DateTime? notBeforeUtc = null,
        bool includeExpiration = true)
    {
        var now = DateTime.UtcNow;

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secret));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var payload = new JwtPayload
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["sub"] = "integration-test@example.com",
            ["userId"] = 123L,
            ["iat"] = new DateTimeOffset(
                now.AddMinutes(-15)).ToUnixTimeSeconds(),
            ["nbf"] = new DateTimeOffset(
                    notBeforeUtc ?? now.AddMinutes(-10))
                .ToUnixTimeSeconds()
        };

        if (role is not null)
        {
            payload["roles"] = new[] { role };
        }

        if (includeExpiration)
        {
            payload["exp"] = new DateTimeOffset(
                    expiresAtUtc ?? now.AddHours(2))
                .ToUnixTimeSeconds();
        }

        var header = new JwtHeader(credentials);

        var token = new JwtSecurityToken(
            header,
            payload);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}