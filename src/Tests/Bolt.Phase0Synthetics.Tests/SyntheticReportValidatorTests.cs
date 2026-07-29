using FluentAssertions;
using XFramework.Bolt.Phase0Synthetics;

namespace Bolt.Phase0Synthetics.Tests;

public sealed class SyntheticReportValidatorTests
{
    private static readonly string[] RequiredOperations =
    [
        "user_registration",
        "hostile_reserved_registration",
        "communications_registration",
        "identity_health_check",
        "transient_presence",
        "durable_offline_registration",
        "durable_offline_publish",
        "durable_ordered_replay",
        "durable_ack",
        "durable_no_redelivery",
        "durable_unregister"
    ];

    [Test]
    public void Validate_CompletePassedReport_Succeeds()
    {
        var report = CreatePassedReport();

        var action = () => SyntheticReportValidator.Validate(report);

        action.Should().NotThrow();
    }

    [Test]
    public void Validate_OptionalTokenEvidenceWithoutOperation_FailsClosed()
    {
        var report = CreatePassedReport() with
        {
            TokenSha256Prefixes = new Dictionary<string, string>
            {
                ["communications_transport"] = "0123456789ab",
                ["communications_identity_service"] = "56789abcdef0",
                ["portal_transport"] = "123456789abc",
                ["portal_identity_service"] = "456789abcdef",
                ["user_actor"] = "23456789abcd",
                ["rejected_portal_transport"] = "3456789abcde"
            }
        };

        var action = () => SyntheticReportValidator.Validate(report);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("A passing synthetic report is missing token evidence coverage.");
    }

    [Test]
    public void Validate_DuplicateOperationName_FailsClosed()
    {
        var report = CreatePassedReport();
        report = report with { Operations = [.. report.Operations, report.Operations[0]] };

        var action = () => SyntheticReportValidator.Validate(report);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Synthetic operation evidence is invalid.");
    }

    [Test]
    public void Validate_IncompleteAcknowledgementEvidence_FailsClosed()
    {
        var report = CreatePassedReport();
        report = report with
        {
            Operations = report.Operations.Select(operation =>
                operation.Name == "durable_ack"
                    ? operation with
                    {
                        Results = new Dictionary<string, string> { ["cumulative_acknowledged"] = "true" }
                    }
                    : operation).ToArray()
        };

        var action = () => SyntheticReportValidator.Validate(report);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("A passing synthetic report has incomplete acknowledgement evidence.");
    }

    private static SyntheticReport CreatePassedReport()
    {
        var started = DateTimeOffset.UtcNow;
        var completed = started.AddSeconds(1);
        return new SyntheticReport(
            SyntheticReportValidator.SchemaVersion,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["communications_transport"] = "0123456789ab",
                ["communications_identity_service"] = "56789abcdef0",
                ["portal_transport"] = "123456789abc",
                ["portal_identity_service"] = "456789abcdef",
                ["user_actor"] = "23456789abcd"
            },
            started,
            completed,
            "wss://bolt.example.test/bolt/ws",
            "passed",
            new SyntheticTimings(1_000),
            RequiredOperations.Select(name => new SyntheticOperationResult(
                name,
                started,
                completed,
                "passed",
                1,
                name == "durable_ack"
                    ? new Dictionary<string, string>
                    {
                        ["cumulative_acknowledged"] = "true",
                        ["duplicate_ack_idempotent"] = "true",
                        ["out_of_order_ack_monotonic"] = "true"
                    }
                    : new Dictionary<string, string> { ["outcome"] = "passed" })).ToArray());
    }
}
