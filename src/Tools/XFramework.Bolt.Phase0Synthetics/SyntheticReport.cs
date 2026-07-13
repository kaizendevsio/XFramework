namespace XFramework.Bolt.Phase0Synthetics;

public sealed record SyntheticTimings(long TotalMs);

public sealed record SyntheticOperationResult(
    string Name,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Status,
    long TimingMs,
    IReadOnlyDictionary<string, string> Results);

public sealed record SyntheticReport(
    string SchemaVersion,
    Guid RunId,
    IReadOnlyDictionary<string, string> TokenSha256Prefixes,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string? Target,
    string Status,
    SyntheticTimings Timings,
    IReadOnlyList<SyntheticOperationResult> Operations);
