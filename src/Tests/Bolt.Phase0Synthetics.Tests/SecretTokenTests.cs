using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using XFramework.Bolt.Phase0Synthetics;

namespace Bolt.Phase0Synthetics.Tests;

public sealed class SecretTokenTests
{
    [Test]
    public void Constructor_TokenValue_ExposesOnlyDeterministicSha256Prefix()
    {
        const string tokenValue = "synthetic-marker-secret-value";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenValue)))[..12]
            .ToLowerInvariant();

        var token = new SecretToken(tokenValue);

        token.Sha256Prefix.Should().Be(expected);
        token.ToString().Should().Be("[REDACTED]");
        token.ToString().Should().NotContain(tokenValue);
    }

    [Test]
    public void Serialize_ReportWithTokenEvidence_DoesNotContainTokenValue()
    {
        const string tokenValue = "never-write-this-token";
        var token = new SecretToken(tokenValue);
        var now = DateTimeOffset.UtcNow;
        var report = new SyntheticReport(
            SyntheticReportValidator.SchemaVersion,
            Guid.NewGuid(),
            new Dictionary<string, string> { ["user"] = token.Sha256Prefix },
            now,
            now,
            "wss://bolt.example.test/bolt/ws",
            "failed",
            new SyntheticTimings(1),
            [new SyntheticOperationResult("input_validation", now, now, "failed", 0,
                new Dictionary<string, string> { ["outcome"] = "invalid_input" })]);

        var json = SyntheticReportWriter.Serialize(report);

        json.Should().Contain(token.Sha256Prefix);
        json.Should().NotContain(tokenValue);
        json.ToLowerInvariant().Should().NotContain("authorization");
        json.ToLowerInvariant().Should().NotContain("accesstoken");
    }
}
