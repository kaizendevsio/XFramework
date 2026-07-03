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

    private static ServiceTokenValidator Validator(TokenFixture fixture) =>
        new(
            new TestSigningKeyProvider(fixture.PublicKeyPem, fixture.KeyId),
            Options.Create(new ServiceIdentityOptions { Issuer = Issuer }));

    private static TokenFixture CreateTokenFixture(
        string audience,
        IReadOnlyCollection<string> scopes,
        DateTime? expiresAtUtc = null)
    {
        using var rsa = RSA.Create(2048);
        var keyId = Guid.NewGuid().ToString("N");
        var securityKey = new RsaSecurityKey(rsa) { KeyId = keyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
        var now = DateTime.UtcNow;
        var expires = expiresAtUtc ?? now.AddMinutes(5);
        var notBefore = expiresAtUtc.HasValue && expiresAtUtc.Value < now
            ? expiresAtUtc.Value.AddMinutes(-5)
            : now.AddMinutes(-1);
        var jwt = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims:
            [
                new Claim("client_id", XFrameworkServiceNames.Portal),
                new Claim(JwtRegisteredClaimNames.Sub, XFrameworkServiceNames.Portal),
                new Claim("scope", string.Join(' ', scopes))
            ],
            notBefore: notBefore,
            expires: expires,
            signingCredentials: credentials);

        jwt.Header["kid"] = keyId;
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new(token, rsa.ExportSubjectPublicKeyInfoPem(), keyId);
    }

    private sealed record TokenFixture(string Token, string PublicKeyPem, string KeyId);

    private sealed class TestSigningKeyProvider(string publicKeyPem, string keyId) : IIdentitySigningKeyProvider
    {
        public Task<IReadOnlyList<ServiceSigningKeyResponse>> GetSigningKeysAsync(
            string? kid = null,
            CancellationToken ct = default)
        {
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
}
