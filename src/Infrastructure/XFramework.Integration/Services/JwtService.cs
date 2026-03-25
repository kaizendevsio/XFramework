using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;
using XFramework.Integration.Abstractions;

namespace XFramework.Integration.Services;

public sealed class JwtService(JwtOptions jwtOptions) : IJwtService
{
    public async Task<JwtToken> GenerateToken(string username, Guid id, List<Guid> Type)
    {
        List<Claim> authClaims =
        [
            new(ClaimTypes.GivenName, username),
            new(ClaimTypes.Role, JsonSerializer.Serialize(Type, new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles })),
            new(ClaimTypes.Name, id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.AuthTime, DateTime.UtcNow.ToString())
        ];

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));

        var token = new JwtSecurityToken(
            issuer: jwtOptions.ValidIssuer,
            audience: jwtOptions.ValidAudience,
            expires: DateTime.UtcNow.AddMinutes(DateTime.Parse(jwtOptions.AccessTokenLifespan).Minute),
            claims: authClaims,
            signingCredentials: new(securityKey, SecurityAlgorithms.HmacSha512)
        );

        var refreshToken = new RefreshToken
        {
            Cuid = id,
            Token = GenerateRefreshToken(),
            ExpireAt = DateTime.UtcNow.AddMinutes(DateTime.Parse(jwtOptions.RefreshTokenLifespan).Minute)
        };

        return new()
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = refreshToken.Token,
            SessionId = Guid.NewGuid()
        };
    }

    public async Task<JwtToken> GenerateToken(List<Claim> claims)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));

        var token = new JwtSecurityToken(
            issuer: jwtOptions.ValidIssuer,
            audience: jwtOptions.ValidAudience,
            expires: DateTime.UtcNow.AddMinutes(DateTime.Parse(jwtOptions.AccessTokenLifespan).Minute),
            claims: claims,
            signingCredentials: new(securityKey, SecurityAlgorithms.HmacSha512)
        );

        var refreshToken = new RefreshToken
        {
            Token = GenerateRefreshToken(),
            ExpireAt = DateTime.UtcNow.AddMinutes(DateTime.Parse(jwtOptions.RefreshTokenLifespan).Minute)
        };

        return new()
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = refreshToken.Token,
            SessionId = Guid.NewGuid()
        };
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var randomNumberGenerator = RandomNumberGenerator.Create();
        randomNumberGenerator.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task<JwtToken> Refresh(string refreshToken, string accessToken, DateTime now)
    {
        var (principal, jwtToken) = await DecodeExpiredToken(accessToken);
        if (jwtToken == null)
        {
            throw new SecurityTokenException("Invalid token");
        }

        return await GenerateToken(principal.Claims.ToList());
    }

    public async Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeJwtToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new SecurityTokenException("Invalid token");
        }

        var principal = new JwtSecurityTokenHandler()
            .ValidateToken(token,
                new()
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.ValidAudience,
                    ValidIssuer = jwtOptions.ValidIssuer,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtOptions.Secret)),
                    RequireExpirationTime = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                },
                out var validatedToken);
        return (principal, validatedToken as JwtSecurityToken);
    }

    public async Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeExpiredToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new SecurityTokenException("Invalid token");
        }

        var principal = new JwtSecurityTokenHandler()
            .ValidateToken(token,
                new()
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.ValidAudience,
                    ValidIssuer = jwtOptions.ValidIssuer,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtOptions.Secret)),
                    RequireExpirationTime = false,
                    ValidateLifetime = false,
                    ClockSkew = TimeSpan.FromMinutes(1)
                },
                out var validatedToken);
        return (principal, validatedToken as JwtSecurityToken);
    }
}
