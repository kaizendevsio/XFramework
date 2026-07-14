using System.Text.Json;
using FluentAssertions;
using XFramework.Bolt.Phase0Synthetics;

namespace Bolt.Phase0Synthetics.Tests;

public sealed class JwtDescriptorReaderTests
{
    [Test]
    public void Read_ServiceTokenClaims_ReturnsExpirationAndServiceName()
    {
        var expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        var token = CreateToken(new Dictionary<string, object>
        {
            ["exp"] = expiration,
            ["iss"] = "XFramework.IdentityServer",
            ["aud"] = "XFramework.Bolt.Hub",
            ["scope"] = "bolt.service",
            ["client_credential_generation"] = "generation-2",
            ["client_id"] = "XFramework.Communications",
            ["service"] = "XFramework.Communications",
            ["sub"] = "XFramework.Communications"
        });

        var descriptor = JwtDescriptorReader.Read(token);

        descriptor.ExpiresAtUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(expiration));
        descriptor.ServiceName.Should().Be("XFramework.Communications");
    }

    [Test]
    public void Read_UserActorTokenClaims_FailsClosed()
    {
        var expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        var token = CreateToken(new Dictionary<string, object>
        {
            ["exp"] = expiration,
            ["scope"] = "openid profile",
            ["sub"] = Guid.NewGuid().ToString("N")
        });

        var action = () => JwtDescriptorReader.Read(token);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("invalid_expiry_token_claims");
    }

    [Test]
    public void Read_MismatchedTransportIdentityClaims_FailsClosed()
    {
        var expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        var token = CreateToken(new Dictionary<string, object>
        {
            ["exp"] = expiration,
            ["iss"] = "XFramework.IdentityServer",
            ["aud"] = "XFramework.Bolt.Hub",
            ["scope"] = "bolt.service",
            ["client_credential_generation"] = "generation-2",
            ["client_id"] = "XFramework.Communications",
            ["service"] = "XFramework.Portal",
            ["sub"] = "XFramework.Communications"
        });

        var action = () => JwtDescriptorReader.Read(token);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("invalid_expiry_token_claims");
    }

    [Test]
    public void Read_MissingExpiration_FailsWithFixedCode()
    {
        var token = CreateToken(new Dictionary<string, object> { ["sub"] = "synthetic-user" });

        var action = () => JwtDescriptorReader.Read(token);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("invalid_expiry_token_claims");
    }

    private static SecretToken CreateToken(IReadOnlyDictionary<string, object> claims)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            kid = "bolt-test-key",
            typ = "bolt+jwt"
        }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        return new SecretToken($"{header}.{payload}.signature");
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
