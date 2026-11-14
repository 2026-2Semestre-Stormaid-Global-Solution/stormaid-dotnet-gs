using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Stormaid.API.IntegrationTests.Helpers;

/// <summary>
/// Gera tokens JWT para cenários controlados de integração.
/// </summary>
public static class JwtTokenFactory
{
    public static string CreateToken(
        string secret,
        string role = "CITIZEN",
        DateTime? expiresAtUtc = null,
        string issuer = "stormaid-api",
        string audience = "stormaid-services")
    {
        var now = DateTime.UtcNow;
        var expiration = expiresAtUtc ?? now.AddHours(2);

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secret));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var header = new JwtHeader(credentials);

        var payload = new JwtPayload
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["sub"] = "integration-test@example.com",
            ["userId"] = 123L,
            ["roles"] = new[] { role },
            ["iat"] = new DateTimeOffset(now).ToUnixTimeSeconds(),
            ["nbf"] = new DateTimeOffset(now.AddMinutes(-1))
                .ToUnixTimeSeconds(),
            ["exp"] = new DateTimeOffset(expiration)
                .ToUnixTimeSeconds()
        };

        var token = new JwtSecurityToken(header, payload);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}