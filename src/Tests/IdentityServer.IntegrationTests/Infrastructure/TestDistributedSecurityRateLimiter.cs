using System.Collections.Concurrent;
using XFramework.Core.RateLimiting;

namespace IdentityServer.IntegrationTests;

public sealed class TestDistributedSecurityRateLimiter : IDistributedSecurityRateLimiter
{
    private readonly ConcurrentQueue<(StrictSecurityRateLimitPolicy Policy, string ClientKey)> _calls = new();
    private DistributedSecurityRateLimitDecision _decision = DistributedSecurityRateLimitDecision.Allowed;
    private Exception? _exception;

    public IReadOnlyCollection<(StrictSecurityRateLimitPolicy Policy, string ClientKey)> Calls =>
        _calls.ToArray();

    public ValueTask<DistributedSecurityRateLimitDecision> AcquireAsync(
        StrictSecurityRateLimitPolicy policy,
        string clientKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Enqueue((policy, clientKey));
        if (_exception is not null)
            throw _exception;

        return ValueTask.FromResult(_decision);
    }

    public void Reset(DistributedSecurityRateLimitDecision? decision = null)
    {
        while (_calls.TryDequeue(out _))
        {
        }

        _decision = decision ?? DistributedSecurityRateLimitDecision.Allowed;
        _exception = null;
    }

    public void ResetWithException(Exception exception)
    {
        Reset();
        _exception = exception;
    }
}
