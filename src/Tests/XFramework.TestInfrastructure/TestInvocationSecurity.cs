using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using XFramework.Domain.Shared.Extensions;
using XFramework.Domain.Shared.Security;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace XFramework.TestInfrastructure;

public sealed class TestEffectiveTenantContextAccessor(Guid tenantId)
    : IEffectiveTenantContextAccessor
{
    public bool HasTrustedInvocation => true;
    public Guid? EffectiveTenantId => tenantId;
}

public sealed class TestCrossTenantWriteAuthorizationAccessor
    : ICrossTenantWriteAuthorizationAccessor
{
    public bool IsAuthorized => true;
}

public sealed class TestBoltTransportAuthority(string baseUrl) : IDisposable
{
    private const string MetadataPath = "/.well-known/openid-configuration";
    private const string JwksPath = "/.well-known/bolt-transport-jwks.json";
    private readonly RSA _signingKey = RSA.Create(3072);

    public void Configure(WebApplicationBuilder builder)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltTransportAuthentication:MetadataAddress"] = $"{baseUrl}{MetadataPath}",
            ["BoltTransportAuthentication:Issuer"] = XFrameworkServiceNames.IdentityServer,
            ["BoltTransportAuthentication:Audience"] = XFrameworkServiceNames.BoltHub,
            ["BoltTransportAuthentication:RequireHttpsMetadata"] = "false",
            ["JwtOptions:GenerationId"] = "integration-tests-g1",
            ["JwtOptions:SigningPublicKeyPath"] = TestJwtKeyMaterial.PublicKeyPath,
            ["JwtOptions:ValidIssuer"] = XFrameworkServiceNames.IdentityServer,
            ["JwtOptions:ValidAudience"] = XFrameworkServiceNames.BoltHub,
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00"
        });
    }

    public void MapEndpoints(WebApplication app)
    {
        var keyId = GetKeyId();
        var publicKey = _signingKey.ExportParameters(includePrivateParameters: false);
        app.MapGet(MetadataPath, () => Results.Json(new
        {
            issuer = XFrameworkServiceNames.IdentityServer,
            jwks_uri = $"{baseUrl}{JwksPath}",
            id_token_signing_alg_values_supported = new[] { SecurityAlgorithms.RsaSha256 }
        }));
        app.MapGet(JwksPath, () => Results.Json(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = keyId,
                    alg = SecurityAlgorithms.RsaSha256,
                    n = Base64UrlEncoder.Encode(publicKey.Modulus!),
                    e = Base64UrlEncoder.Encode(publicKey.Exponent!)
                }
            }
        }));
    }

    public IBoltTransportTokenProvider CreateTokenProvider(string clientId) =>
        new TestBoltTransportTokenProvider(this, clientId);

    public void Dispose() => _signingKey.Dispose();

    private string Issue(string clientId)
    {
        var issuedAt = DateTime.UtcNow;
        List<Claim> claims =
        [
            new("client_id", clientId),
            new("service", clientId),
            new(JwtRegisteredClaimNames.Sub, clientId),
            new("scope", XFrameworkServiceScopes.BoltService),
            new("client_credential_generation", "integration-tests-g1"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(issuedAt).ToString(),
                ClaimValueTypes.Integer64)
        ];
        var token = new JwtSecurityToken(
            issuer: XFrameworkServiceNames.IdentityServer,
            audience: XFrameworkServiceNames.BoltHub,
            claims: claims,
            notBefore: issuedAt,
            expires: issuedAt.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(_signingKey) { KeyId = GetKeyId() },
                SecurityAlgorithms.RsaSha256));
        token.Header["typ"] = "bolt+jwt";
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GetKeyId() =>
        $"bolt-{Base64UrlEncoder.Encode(SHA256.HashData(_signingKey.ExportSubjectPublicKeyInfo()))}";

    private sealed class TestBoltTransportTokenProvider(
        TestBoltTransportAuthority authority,
        string clientId) : IBoltTransportTokenProvider
    {
        public ValueTask<string> GetTokenAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(authority.Issue(clientId));
        }
    }
}

public sealed record TestInvocationIdentityOptions(
    string ActorToken,
    string ServiceToken,
    string ServiceClientId,
    Guid TenantId,
    Guid CredentialId,
    Guid IdentityId,
    Guid SessionId);

public static class TestInvocationIdentityExtensions
{
    private const string ActorTokenPrefix = "xfw-test-actor.";

    public static string CreateTestActorToken(
        Guid tenantId,
        Guid credentialId,
        Guid identityId,
        Guid sessionId,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string>? capabilities = null)
    {
        var payload = new TestActorTokenPayload(
            tenantId,
            credentialId,
            identityId,
            sessionId,
            roles,
            capabilities ?? []);
        return ActorTokenPrefix + Base64UrlEncoder.Encode(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
    }

    internal static bool TryParseTestActorToken(string token, out TrustedActorIdentity actor)
    {
        actor = null!;
        if (!token.StartsWith(ActorTokenPrefix, StringComparison.Ordinal))
            return false;

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(token[ActorTokenPrefix.Length..]));
            var payload = JsonSerializer.Deserialize<TestActorTokenPayload>(json);
            if (payload is null ||
                payload.TenantId == Guid.Empty ||
                payload.CredentialId == Guid.Empty ||
                payload.IdentityId == Guid.Empty ||
                payload.SessionId == Guid.Empty)
            {
                return false;
            }

            actor = new TrustedActorIdentity(
                payload.CredentialId,
                payload.IdentityId,
                payload.TenantId,
                payload.SessionId,
                new HashSet<string>(payload.Roles, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(payload.Capabilities, StringComparer.OrdinalIgnoreCase),
                "integration-tests-g1",
                DateTimeOffset.UtcNow.AddHours(8));
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }

    public static IServiceCollection AddTestInvocationServer(
        this IServiceCollection services,
        TestInvocationIdentityOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IActorIdentityProvider, TestActorIdentityProvider>();
        services.AddSingleton<IServiceIdentityProvider, TestServiceIdentityProvider>();
        return services;
    }

    public static IServiceCollection AddTestInvocationClient(
        this IServiceCollection services,
        TestInvocationIdentityOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IActorAccessTokenProvider, TestActorAccessTokenProvider>();
        services.AddSingleton<IServiceTokenProvider, TestServiceTokenProvider>();
        return services;
    }

    private sealed record TestActorTokenPayload(
        Guid TenantId,
        Guid CredentialId,
        Guid IdentityId,
        Guid SessionId,
        IReadOnlyCollection<string> Roles,
        IReadOnlyCollection<string> Capabilities);
}

public static class TestInvocationActorTokenScope
{
    private static readonly AsyncLocal<string?> CurrentToken = new();

    public static IDisposable Push(string actorToken)
    {
        var previous = CurrentToken.Value;
        CurrentToken.Value = actorToken;
        return new Scope(previous);
    }

    internal static string? Current => CurrentToken.Value;

    private sealed class Scope(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            CurrentToken.Value = previous;
            _disposed = true;
        }
    }
}

internal sealed class TestActorAccessTokenProvider(TestInvocationIdentityOptions options)
    : IActorAccessTokenProvider
{
    public ValueTask<string?> GetTokenAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult<string?>(TestInvocationActorTokenScope.Current ?? options.ActorToken);
    }
}

internal sealed class TestServiceTokenProvider(TestInvocationIdentityOptions options)
    : IServiceTokenProvider
{
    public ValueTask<string> GetTokenAsync(
        string audience,
        IReadOnlyCollection<string>? scopes = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(options.ServiceToken);
    }
}

internal sealed class TestActorIdentityProvider(TestInvocationIdentityOptions options)
    : IActorIdentityProvider
{
    public Task<ActorIdentityValidationResult> ValidateAsync(
        string token,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (TestInvocationIdentityExtensions.TryParseTestActorToken(token, out var encodedActor))
            return Task.FromResult(ActorIdentityValidationResult.Success(encodedActor));

        if (!string.Equals(token, options.ActorToken, StringComparison.Ordinal))
            return Task.FromResult(ActorIdentityValidationResult.Failure("Invalid test actor token."));

        return Task.FromResult(ActorIdentityValidationResult.Success(new TrustedActorIdentity(
            options.CredentialId,
            options.IdentityId,
            options.TenantId,
            options.SessionId,
            new HashSet<string>(["IntegrationAdministrator", "Admin"], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(
                ["identity.tenants:manage", "inventario.override_expired_lot"],
                StringComparer.OrdinalIgnoreCase),
            "integration-tests-g1",
            DateTimeOffset.UtcNow.AddHours(8))));
    }
}

internal sealed class TestServiceIdentityProvider(TestInvocationIdentityOptions options)
    : IServiceIdentityProvider
{
    public Task<ServiceIdentityValidationResult> ValidateAsync(
        string token,
        string expectedAudience,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(string.Equals(token, options.ServiceToken, StringComparison.Ordinal)
            ? ServiceIdentityValidationResult.Success(new TrustedServiceIdentity(
                options.ServiceClientId,
                expectedAudience,
                new HashSet<string>(XFrameworkServiceScopes.AdminDefaults, StringComparer.Ordinal),
                "integration-tests-g1"))
            : ServiceIdentityValidationResult.Failure("Invalid test service token."));
    }
}
