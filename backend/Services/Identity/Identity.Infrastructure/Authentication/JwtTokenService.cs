using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Identity.Application.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Authentication;

public sealed class JwtSettings
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;

    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var key = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be provided through a secret configuration source and contain at least 32 bytes.");
        }

        return new JwtSettings
        {
            Issuer = configuration["Jwt:Issuer"] ?? "racehorse-identity",
            Audience = configuration["Jwt:Audience"] ?? "racehorse-api",
            SigningKey = key,
            AccessTokenMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15),
            RefreshTokenDays = configuration.GetValue("Jwt:RefreshTokenDays", 30)
        };
    }
}

public sealed class JwtTokenService(JwtSettings settings) : IIdentityTokenService
{
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public DateTimeOffset AccessTokenExpiry => DateTimeOffset.UtcNow.AddMinutes(settings.AccessTokenMinutes);

    public DateTimeOffset RefreshTokenExpiry => DateTimeOffset.UtcNow.AddDays(settings.RefreshTokenDays);

    public IssuedAccessToken IssueAccessToken(AuthUser user)
    {
        var expiresAt = AccessTokenExpiry;
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.FullName),
                new Claim("role", user.RoleCode),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _credentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string CreateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}