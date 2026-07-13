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
            ["scope"] = "openid bolt.service",
            ["client_id"] = "XFramework.Communications"
        });

        var descriptor = JwtDescriptorReader.Read(token);

        descriptor.ExpiresAtUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(expiration));
        descriptor.ServiceName.Should().Be("XFramework.Communications");
    }

    [Test]
    public void Read_UserTokenClaims_ReturnsNullServiceName()
    {
        var expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        var token = CreateToken(new Dictionary<string, object>
        {
            ["exp"] = expiration,
            ["scope"] = "openid profile",
            ["sub"] = Guid.NewGuid().ToString("N")
        });

        var descriptor = JwtDescriptorReader.Read(token);

        descriptor.ServiceName.Should().BeNull();
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
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "none", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        return new SecretToken($"{header}.{payload}.signature");
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
