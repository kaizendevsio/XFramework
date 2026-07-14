using System.Text.Json;
using FluentAssertions;
using XFramework.Bolt.Phase0Synthetics;

namespace Bolt.Phase0Synthetics.Tests;

public sealed class SyntheticReportWriterTests
{
    [Test]
    public void Serialize_Report_ContainsOnlyApprovedTopLevelFields()
    {
        var now = DateTimeOffset.UtcNow;
        var report = new SyntheticReport(
            SyntheticReportValidator.SchemaVersion,
            Guid.NewGuid(),
            new Dictionary<string, string> { ["user_actor"] = "0123456789ab" },
            now,
            now.AddMilliseconds(12),
            "wss://bolt.example.test/bolt/ws",
            "failed",
            new SyntheticTimings(12),
            [
                new SyntheticOperationResult(
                    "identity_health_check",
                    now,
                    now.AddMilliseconds(12),
                    "failed",
                    12,
                    new Dictionary<string, string> { ["outcome"] = "health_query_response_invalid" })
            ]);

        var json = SyntheticReportWriter.Serialize(report);
        using var document = JsonDocument.Parse(json);

        document.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "schemaVersion",
            "runId",
            "tokenSha256Prefixes",
            "startedAtUtc",
            "completedAtUtc",
            "target",
            "status",
            "timings",
            "operations");
        json.ToLowerInvariant().Should().NotContain("exception");
        json.ToLowerInvariant().Should().NotContain("stacktrace");
    }

    [Test]
    public async Task WriteAsync_Report_WritesExactlyOneJsonDocumentLine()
    {
        var now = DateTimeOffset.UtcNow;
        var report = new SyntheticReport(
            SyntheticReportValidator.SchemaVersion,
            Guid.NewGuid(),
            new Dictionary<string, string>(),
            now,
            now,
            null,
            "failed",
            new SyntheticTimings(0),
            [new SyntheticOperationResult("input_validation", now, now, "failed", 0,
                new Dictionary<string, string> { ["outcome"] = "invalid_input" })]);
        using var writer = new StringWriter();

        await SyntheticReportWriter.WriteAsync(writer, report);

        var lines = writer.ToString().Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.Should().ContainSingle();
        using var document = JsonDocument.Parse(lines[0]);
        document.RootElement.GetProperty("target").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Test]
    public void Serialize_PassedReportMissingRequiredOperations_FailsClosed()
    {
        var now = DateTimeOffset.UtcNow;
        var report = new SyntheticReport(
            SyntheticReportValidator.SchemaVersion,
            Guid.NewGuid(),
            new Dictionary<string, string> { ["user_actor"] = "0123456789ab" },
            now,
            now,
            "wss://bolt.example.test/bolt/ws",
            "passed",
            new SyntheticTimings(0),
            []);

        var action = () => SyntheticReportWriter.Serialize(report);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("A passing synthetic report is incomplete.");
    }

    [TestCase("eyJhbGciOiJub25lIn0.payload.signature")]
    [TestCase("Bearer secret")]
    [TestCase("value with whitespace")]
    public void Serialize_UnsafeOperationResult_FailsClosed(string value)
    {
        var now = DateTimeOffset.UtcNow;
        var report = new SyntheticReport(
            SyntheticReportValidator.SchemaVersion,
            Guid.NewGuid(),
            new Dictionary<string, string>(),
            now,
            now,
            null,
            "failed",
            new SyntheticTimings(0),
            [new SyntheticOperationResult("input_validation", now, now, "failed", 0,
                new Dictionary<string, string> { ["outcome"] = value })]);

        var action = () => SyntheticReportWriter.Serialize(report);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Synthetic operation result is invalid.");
    }
}
