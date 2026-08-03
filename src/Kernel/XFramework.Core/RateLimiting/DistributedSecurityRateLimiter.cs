namespace XFramework.Core.RateLimiting;

public readonly record struct DistributedSecurityRateLimitDecision(
    bool IsAllowed,
    TimeSpan RetryAfter)
{
    public static DistributedSecurityRateLimitDecision Allowed { get; } = new(true, TimeSpan.Zero);

    public static DistributedSecurityRateLimitDecision Rejected(TimeSpan retryAfter) =>
        new(false, retryAfter);
}

public interface IDistributedSecurityRateLimiter
{
    ValueTask<DistributedSecurityRateLimitDecision> AcquireAsync(
        StrictSecurityRateLimitPolicy policy,
        string clientKey,
        CancellationToken cancellationToken);
}

internal sealed class DisabledDistributedSecurityRateLimiter : IDistributedSecurityRateLimiter
{
    public ValueTask<DistributedSecurityRateLimitDecision> AcquireAsync(
        StrictSecurityRateLimitPolicy policy,
        string clientKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(DistributedSecurityRateLimitDecision.Allowed);
    }
}
