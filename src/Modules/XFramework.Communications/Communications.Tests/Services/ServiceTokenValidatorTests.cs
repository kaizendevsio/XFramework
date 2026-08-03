using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Communications.Tests.Services;

public sealed class ServiceTokenValidatorTests
{
    private const string Issuer = "XFramework.IdentityServer";
    private const string CurrentGeneration = "generation-1";

    [Test]
    public async Task ValidateAsync_ValidServiceToken_ReturnsCallerAndScopes()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);
        var validator = Validator(fixture);

        var result = await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);

        Assert.That(result.IsValid, Is.True, result.Error);
        Assert.That(result.CallerClientId, Is.EqualTo(XFrameworkServiceNames.Portal));
        Assert.That(result.Scopes, Contains.Item(XFrameworkServiceScopes.CommunicationsAdmin));
    }

    [Test]
    public async Task ValidateAsync_WrongAudience_ReturnsInvalid()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Wallets,
            [XFrameworkServiceScopes.CommunicationsAdmin]);
        var validator = Validator(fixture);

        var result = await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task ValidateAsync_MissingRequiredScope_ReturnsInvalid()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.BoltService]);
        var validator = Validator(fixture);

        var result = await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Error, Does.Contain("missing required scope"));
    }

    [Test]
    public async Task ValidateAsync_CallerGenerationDifferentFromReceiver_ReturnsValid()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin],
            generationId: "retired-generation");

        var result = await Validator(fixture).ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications);

        Assert.That(result.IsValid, Is.True, result.Error);
    }

    [Test]
    public async Task ValidateAsync_MissingCredentialGeneration_ReturnsInvalid()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin],
            generationId: null);

        var result = await Validator(fixture).ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task ValidateAsync_CallerGenerationMatchingReceiverFallback_ReturnsValid()
    {
        const string fallbackGeneration = "generation-0";
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin],
            generationId: fallbackGeneration);
        var validator = new ServiceTokenValidator(
            new TestSigningKeyProvider(fixture.PublicKeyPem, fixture.KeyId),
            Options.Create(new ServiceIdentityOptions
            {
                Issuer = Issuer,
                GenerationId = CurrentGeneration,
                ValidationFallback = new ServiceIdentityValidationFallbackOptions
                {
                    GenerationId = fallbackGeneration,
                    ClientSecret = "not-used-for-token-validation",
                    ValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes(5)
                }
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceTokenValidator>.Instance);

        var result = await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications);

        Assert.That(result.IsValid, Is.True, result.Error);
    }

    [Test]
    public async Task ValidateAsync_ExpiredToken_ReturnsInvalid()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin],
            expiresAtUtc: DateTime.UtcNow.AddMinutes(-5));
        var validator = Validator(fixture);

        var result = await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task ValidateAsync_MalformedToken_ReturnsInvalid()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);

        var result = await Validator(fixture).ValidateAsync(
            "not-a-jwt",
            XFrameworkServiceNames.Communications);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task ValidateAsync_WrongSignature_ReturnsInvalid()
    {
        var trustedKeys = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);
        var attackerToken = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);
        var validator = new ServiceTokenValidator(
            new TestSigningKeyProvider(trustedKeys.PublicKeyPem, attackerToken.KeyId),
            Options.Create(ValidationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceTokenValidator>.Instance);

        var result = await validator.ValidateAsync(
            attackerToken.Token,
            XFrameworkServiceNames.Communications);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task ValidateAsync_FailedValidation_IsNotCached()
    {
        var trustedKeys = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);
        var attackerToken = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);
        var keyProvider = new TestSigningKeyProvider(trustedKeys.PublicKeyPem, attackerToken.KeyId);
        var validator = new ServiceTokenValidator(
            keyProvider,
            Options.Create(ValidationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceTokenValidator>.Instance);

        var first = await validator.ValidateAsync(attackerToken.Token, XFrameworkServiceNames.Communications);
        var second = await validator.ValidateAsync(attackerToken.Token, XFrameworkServiceNames.Communications);

        Assert.That(first.IsValid, Is.False);
        Assert.That(second.IsValid, Is.False);
        Assert.That(keyProvider.RequestCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ValidateAsync_ReusesSuccessfulBaseValidation()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);
        var keyProvider = new TestSigningKeyProvider(fixture.PublicKeyPem, fixture.KeyId);
        var validator = new ServiceTokenValidator(
            keyProvider,
            Options.Create(ValidationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceTokenValidator>.Instance);

        var first = await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications);
        keyProvider.FailRequests = true;
        var second = await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);

        Assert.That(first.IsValid, Is.True, first.Error);
        Assert.That(second.IsValid, Is.True, second.Error);
        Assert.That(keyProvider.RequestCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ValidateAsync_SuccessfulCache_DoesNotCoupleCallerToReceiverCredentialRotation()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);
        var keyProvider = new TestSigningKeyProvider(fixture.PublicKeyPem, fixture.KeyId);
        var validationOptions = ValidationOptions();
        var validator = new ServiceTokenValidator(
            keyProvider,
            Options.Create(validationOptions),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceTokenValidator>.Instance);

        var first = await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications);
        validationOptions.GenerationId = "generation-2";
        keyProvider.FailRequests = true;
        var second = await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications);

        Assert.That(first.IsValid, Is.True, first.Error);
        Assert.That(second.IsValid, Is.True, second.Error);
        Assert.That(keyProvider.RequestCount, Is.EqualTo(1), "receiver credential rotation must not invalidate a different caller's signed token");
    }

    [Test]
    public async Task ValidateAsync_DistinctTokensSignedBySameKey_BothValidate()
    {
        using var rsa = RSA.Create(2048);
        var keyId = Guid.NewGuid().ToString("N");
        var keyProvider = new TestSigningKeyProvider(rsa.ExportSubjectPublicKeyInfoPem(), keyId);
        var validator = new ServiceTokenValidator(
            keyProvider,
            Options.Create(ValidationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceTokenValidator>.Instance);
        var portalToken = CreateToken(
            rsa,
            keyId,
            XFrameworkServiceNames.IdentityServer,
            XFrameworkServiceNames.Portal,
            [XFrameworkServiceScopes.BoltService]);
        var communicationsToken = CreateToken(
            rsa,
            keyId,
            XFrameworkServiceNames.IdentityServer,
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.BoltService]);

        var portal = await validator.ValidateAsync(
            portalToken,
            XFrameworkServiceNames.IdentityServer,
            [XFrameworkServiceScopes.BoltService]);
        var communications = await validator.ValidateAsync(
            communicationsToken,
            XFrameworkServiceNames.IdentityServer,
            [XFrameworkServiceScopes.BoltService]);

        Assert.That(portal.IsValid, Is.True, portal.Error);
        Assert.That(communications.IsValid, Is.True, communications.Error);
        Assert.That(communications.CallerClientId, Is.EqualTo(XFrameworkServiceNames.Communications));
        Assert.That(keyProvider.RequestCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ValidateAsync_SuccessfulCache_RevalidatesAfterJwtExpiry()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin],
            expiresAtUtc: DateTime.UtcNow.AddSeconds(1));
        var keyProvider = new TestSigningKeyProvider(fixture.PublicKeyPem, fixture.KeyId);
        var validator = new ServiceTokenValidator(
            keyProvider,
            Options.Create(ValidationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceTokenValidator>.Instance);

        var first = await validator.ValidateAsync(fixture.Token, XFrameworkServiceNames.Communications);
        await Task.Delay(TimeSpan.FromMilliseconds(1_200));
        var second = await validator.ValidateAsync(fixture.Token, XFrameworkServiceNames.Communications);

        Assert.That(first.IsValid, Is.True, first.Error);
        Assert.That(second.IsValid, Is.True, "JWT clock skew still applies during revalidation");
        Assert.That(keyProvider.RequestCount, Is.EqualTo(2), "expired cache entries must be revalidated");
    }

    [Test]
    public void ValidateAsync_CallerCancellation_IsPropagated()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);
        var validator = new ServiceTokenValidator(
            new CancelingSigningKeyProvider(),
            Options.Create(ValidationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceTokenValidator>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = async () => await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications,
            ct: cancellation.Token);

        Assert.That(act, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task ValidateAsync_RsaSigningKeyBelow2048Bits_IsRejected()
    {
        var fixture = CreateTokenFixture(
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.CommunicationsAdmin]);
        using var weakRsa = RSA.Create(1024);
        var validator = new ServiceTokenValidator(
            new TestSigningKeyProvider(weakRsa.ExportSubjectPublicKeyInfoPem(), fixture.KeyId),
            Options.Create(ValidationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceTokenValidator>.Instance);

        var result = await validator.ValidateAsync(
            fixture.Token,
            XFrameworkServiceNames.Communications);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.FailureStatusCode, Is.EqualTo(503));
        Assert.That(result.Error, Is.EqualTo("Service signing keys are unavailable."));
    }

    private static ServiceTokenValidator Validator(TokenFixture fixture) =>
        new(
            new TestSigningKeyProvider(fixture.PublicKeyPem, fixture.KeyId),
            Options.Create(ValidationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceTokenValidator>.Instance);

    private static TokenFixture CreateTokenFixture(
        string audience,
        IReadOnlyCollection<string> scopes,
        DateTime? expiresAtUtc = null,
        string? generationId = CurrentGeneration)
    {
        using var rsa = RSA.Create(2048);
        var keyId = Guid.NewGuid().ToString("N");
        var token = CreateToken(
            rsa,
            keyId,
            audience,
            XFrameworkServiceNames.Portal,
            scopes,
            expiresAtUtc,
            generationId);
        return new(token, rsa.ExportSubjectPublicKeyInfoPem(), keyId);
    }

    private static string CreateToken(
        RSA rsa,
        string keyId,
        string audience,
        string caller,
        IReadOnlyCollection<string> scopes,
        DateTime? expiresAtUtc = null,
        string? generationId = CurrentGeneration)
    {
        var securityKey = new RsaSecurityKey(rsa) { KeyId = keyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
        var now = DateTime.UtcNow;
        var expires = expiresAtUtc ?? now.AddMinutes(5);
        var notBefore = expiresAtUtc.HasValue && expiresAtUtc.Value < now
            ? expiresAtUtc.Value.AddMinutes(-5)
            : now.AddMinutes(-1);
        List<Claim> claims =
        [
            new("client_id", caller),
            new(JwtRegisteredClaimNames.Sub, caller),
            new("scope", string.Join(' ', scopes))
        ];
        if (generationId is not null)
            claims.Add(new Claim("client_credential_generation", generationId));

        var jwt = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: credentials);

        jwt.Header["kid"] = keyId;
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed record TokenFixture(string Token, string PublicKeyPem, string KeyId);

    private static ServiceIdentityOptions ValidationOptions() => new()
    {
        Issuer = Issuer,
        GenerationId = CurrentGeneration
    };

    private sealed class TestSigningKeyProvider(string publicKeyPem, string keyId) : IIdentitySigningKeyProvider
    {
        public int RequestCount { get; private set; }
        public bool FailRequests { get; set; }

        public Task<IReadOnlyList<ServiceSigningKeyResponse>> GetSigningKeysAsync(
            string? kid = null,
            CancellationToken ct = default)
        {
            RequestCount++;
            if (FailRequests)
                throw new InvalidOperationException("Signing key provider should not be called.");

            IReadOnlyList<ServiceSigningKeyResponse> keys =
            [
                new()
                {
                    KeyId = keyId,
                    Algorithm = "RS256",
                    PublicKeyPem = publicKeyPem,
                    CreatedAtUtc = DateTime.UtcNow,
                    ActivatedAtUtc = DateTime.UtcNow,
                    IsActive = true
                }
            ];

            return Task.FromResult(keys);
        }
    }

    private sealed class CancelingSigningKeyProvider : IIdentitySigningKeyProvider
    {
        public Task<IReadOnlyList<ServiceSigningKeyResponse>> GetSigningKeysAsync(
            string? keyId = null,
            CancellationToken ct = default) => Task.FromCanceled<IReadOnlyList<ServiceSigningKeyResponse>>(ct);
    }
}
