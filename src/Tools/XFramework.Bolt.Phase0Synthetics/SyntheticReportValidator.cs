using System.Text.RegularExpressions;

namespace XFramework.Bolt.Phase0Synthetics;

public static partial class SyntheticReportValidator
{
    public const string SchemaVersion = "bolt-phase0-synthetic-report/v1";

    private static readonly HashSet<string> AllowedStatuses = ["passed", "failed"];
    private static readonly HashSet<string> RequiredPassedOperations =
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

    public static void Validate(SyntheticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!string.Equals(report.SchemaVersion, SchemaVersion, StringComparison.Ordinal) ||
            report.RunId == Guid.Empty ||
            !AllowedStatuses.Contains(report.Status) ||
            report.CompletedAtUtc < report.StartedAtUtc ||
            report.Timings.TotalMs < 0 ||
            report.Timings.TotalMs > (long)(report.CompletedAtUtc - report.StartedAtUtc).TotalMilliseconds + 1_000)
        {
            throw new InvalidOperationException("Synthetic report metadata is invalid.");
        }

        if (report.Target is not null)
        {
            if (!Uri.TryCreate(report.Target, UriKind.Absolute, out var target))
                throw new InvalidOperationException("Synthetic report target is invalid.");
            SyntheticOptionsValidator.ValidateTarget(target);
        }

        ValidateTokenEvidence(report.TokenSha256Prefixes);
        ValidateOperations(report);
    }

    private static void ValidateTokenEvidence(IReadOnlyDictionary<string, string> evidence)
    {
        var allowedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "communications", "user", "expiry", "rejected_communications", "rejected_user"
        };
        foreach (var (name, value) in evidence)
        {
            if (!allowedNames.Contains(name) || !Sha256PrefixRegex().IsMatch(value))
                throw new InvalidOperationException("Synthetic token evidence is invalid.");
        }
    }

    private static void ValidateOperations(SyntheticReport report)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in report.Operations)
        {
            if (!names.Add(operation.Name) ||
                !SafeNameRegex().IsMatch(operation.Name) ||
                !AllowedStatuses.Contains(operation.Status) ||
                operation.StartedAtUtc < report.StartedAtUtc ||
                operation.CompletedAtUtc < operation.StartedAtUtc ||
                operation.CompletedAtUtc > report.CompletedAtUtc.AddSeconds(1) ||
                operation.TimingMs < 0)
            {
                throw new InvalidOperationException("Synthetic operation evidence is invalid.");
            }

            foreach (var (name, value) in operation.Results)
            {
                if (!SafeNameRegex().IsMatch(name) || !SafeValueRegex().IsMatch(value))
                    throw new InvalidOperationException("Synthetic operation result is invalid.");
            }
        }

        if (string.Equals(report.Status, "passed", StringComparison.Ordinal))
        {
            if (report.Target is null || report.Operations.Any(static operation => operation.Status != "passed") ||
                !RequiredPassedOperations.IsSubsetOf(names) ||
                !report.TokenSha256Prefixes.ContainsKey("communications") ||
                !report.TokenSha256Prefixes.ContainsKey("user"))
            {
                throw new InvalidOperationException("A passing synthetic report is incomplete.");
            }

            var durableAck = report.Operations.Single(static operation => operation.Name == "durable_ack");
            if (!IsTrue(durableAck.Results, "cumulative_acknowledged") ||
                !IsTrue(durableAck.Results, "duplicate_ack_idempotent") ||
                !IsTrue(durableAck.Results, "out_of_order_ack_monotonic"))
            {
                throw new InvalidOperationException("A passing synthetic report has incomplete acknowledgement evidence.");
            }

            RequireOperationForEvidence(report.TokenSha256Prefixes, names, "expiry", "token_expiry_disconnect");
            RequireOperationForEvidence(
                report.TokenSha256Prefixes,
                names,
                "rejected_communications",
                "old_generation_communications_token_rejection");
            RequireOperationForEvidence(
                report.TokenSha256Prefixes,
                names,
                "rejected_user",
                "old_generation_user_token_rejection");
        }
        else if (report.Operations.Count == 0 || report.Operations.All(static operation => operation.Status == "passed"))
        {
            throw new InvalidOperationException("A failed synthetic report must contain a failed operation.");
        }
    }

    private static void RequireOperationForEvidence(
        IReadOnlyDictionary<string, string> evidence,
        IReadOnlySet<string> operationNames,
        string evidenceName,
        string operationName)
    {
        if (evidence.ContainsKey(evidenceName) && !operationNames.Contains(operationName))
            throw new InvalidOperationException("A passing synthetic report is missing token evidence coverage.");
    }

    private static bool IsTrue(IReadOnlyDictionary<string, string> results, string name) =>
        results.TryGetValue(name, out var value) && string.Equals(value, "true", StringComparison.Ordinal);

    [GeneratedRegex("^[0-9a-f]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256PrefixRegex();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeNameRegex();

    [GeneratedRegex("^[a-z0-9_./:-]{1,96}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeValueRegex();
}
