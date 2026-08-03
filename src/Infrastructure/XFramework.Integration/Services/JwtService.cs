using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Hosting;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Security;

namespace XFramework.Integration.Services;

public sealed class JwtService : IJwtService
{
    private readonly JwtOptions _jwtOptions;
    private readonly TimeProvider _timeProvider;

    public JwtService(
        JwtOptions jwtOptions,
        IHostEnvironment hostEnvironment,
        TimeProvider? timeProvider = null)
        : this(jwtOptions, hostEnvironment.EnvironmentName, timeProvider)
    {
    }

    public JwtService(JwtOptions jwtOptions, TimeProvider? timeProvider = null)
        : this(jwtOptions, Environments.Production, timeProvider)
    {
    }

    private JwtService(
        JwtOptions jwtOptions,
        string environmentName,
        TimeProvider? timeProvider)
    {
        _jwtOptions = jwtOptions;
        _timeProvider = timeProvider ?? TimeProvider.System;
        JwtCredentialSet.Validate(
            jwtOptions,
            _timeProvider.GetUtcNow(),
            environmentName);
    }

    public async Task<JwtToken> GenerateToken(string username, Guid id, List<Guid> Type, Guid? tenantId = null)
    {
        var sessionId = Guid.NewGuid();
        List<Claim> authClaims =
        [
            new(ClaimTypes.GivenName, username),
            new(ClaimTypes.Role, JsonSerializer.Serialize(Type, new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles })),
            new(ClaimTypes.Name, id.ToString()),
            new("credential_id", id.ToString("D")),
            new(JwtCredentialSet.GenerationClaim, _jwtOptions.GenerationId.Trim()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.AuthTime, _timeProvider.GetUtcNow().UtcDateTime.ToString("O")),
            new("session_id", sessionId.ToString("D"))
        ];

        if (tenantId is Guid resolvedTenantId && resolvedTenantId != Guid.Empty)
        {
            authClaims.Add(new("tenant_id", resolvedTenantId.ToString("D")));
            authClaims.Add(new("tenantId", resolvedTenantId.ToString("D")));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var securityKey = JwtCredentialSet.CreateCurrentSigningKey(_jwtOptions);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.ValidIssuer,
            audience: _jwtOptions.ValidAudience,
            notBefore: now,
            expires: now.Add(ParseLifespan(_jwtOptions.AccessTokenLifespan, TimeSpan.FromMinutes(30))),
            claims: authClaims,
            signingCredentials: new(securityKey, SecurityAlgorithms.RsaSha512)
        );

        var refreshToken = new RefreshToken
        {
            Cuid = id,
            Token = GenerateRefreshToken(),
            ExpireAt = now.Add(ParseLifespan(_jwtOptions.RefreshTokenLifespan, TimeSpan.FromMinutes(30)))
        };

        return new()
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAt = refreshToken.ExpireAt,
            SessionId = sessionId
        };
    }

    public async Task<JwtToken> GenerateToken(List<Claim> claims)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenClaims = claims
            .Where(static claim => claim.Type != JwtCredentialSet.GenerationClaim)
            .ToList();
        tokenClaims.Add(new Claim(JwtCredentialSet.GenerationClaim, _jwtOptions.GenerationId.Trim()));
        var sessionId = tokenClaims
            .Where(static claim => claim.Type == "session_id")
            .Select(static claim => Guid.TryParse(claim.Value, out var value) ? value : Guid.Empty)
            .FirstOrDefault();
        if (sessionId == Guid.Empty)
        {
            sessionId = Guid.NewGuid();
            tokenClaims.Add(new Claim("session_id", sessionId.ToString("D")));
        }

        var securityKey = JwtCredentialSet.CreateCurrentSigningKey(_jwtOptions);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.ValidIssuer,
            audience: _jwtOptions.ValidAudience,
            notBefore: now,
            expires: now.Add(ParseLifespan(_jwtOptions.AccessTokenLifespan, TimeSpan.FromMinutes(30))),
            claims: tokenClaims,
            signingCredentials: new(securityKey, SecurityAlgorithms.RsaSha512)
        );

        var refreshToken = new RefreshToken
        {
            Token = GenerateRefreshToken(),
            ExpireAt = now.Add(ParseLifespan(_jwtOptions.RefreshTokenLifespan, TimeSpan.FromMinutes(30)))
        };

        return new()
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAt = refreshToken.ExpireAt,
            SessionId = sessionId
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
                JwtCredentialSet.CreateValidationParameters(_jwtOptions, validateLifetime: true, _timeProvider),
                out var validatedToken);
        if (validatedToken is not JwtSecurityToken jwtToken)
            throw new SecurityTokenException("Validated token is not a JWT.");

        return (principal, jwtToken);
    }

    public async Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeExpiredToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new SecurityTokenException("Invalid token");
        }

        var principal = new JwtSecurityTokenHandler()
            .ValidateToken(token,
                JwtCredentialSet.CreateValidationParameters(_jwtOptions, validateLifetime: false, _timeProvider),
                out var validatedToken);
        if (validatedToken is not JwtSecurityToken jwtToken)
            throw new SecurityTokenException("Validated token is not a JWT.");

        return (principal, jwtToken);
    }

    private static TimeSpan ParseLifespan(string value, TimeSpan fallback) =>
        TimeSpan.TryParse(value, out var parsed) && parsed > TimeSpan.Zero ? parsed : fallback;

}
