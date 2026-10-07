using StackExchange.Redis;
using XFramework.Core.RateLimiting;

namespace POS.Api.Services;

// Reuse the POS Redis connection. A missing/unavailable security store must not allow guesses.
public sealed class PosScannerClaimRateLimiter(Func<IConnectionMultiplexer?> connection)
    : IDistributedSecurityRateLimiter
{
    private const string Script = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end
        return { count, redis.call('PTTL', KEYS[1]) }
        """;

    public async ValueTask<DistributedSecurityRateLimitDecision> AcquireAsync(
        StrictSecurityRateLimitPolicy policy, string clientKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var redis = connection();
        if (redis is null) throw new InvalidOperationException("Scanner claim limiter is unavailable.");
        var result = await redis.GetDatabase().ScriptEvaluateAsync(Script,
            [(RedisKey)$"xframework:pos:scanner:claims:{policy.Name}:{clientKey}"],
            [(RedisValue)(long)policy.Window.TotalMilliseconds]).WaitAsync(cancellationToken);
        var values = (RedisResult[]?)result;
        if (values is not { Length: 2 })
            throw new InvalidOperationException("Scanner claim limiter returned an invalid response.");
        return (long)values[0] <= policy.PermitLimit
            ? DistributedSecurityRateLimitDecision.Allowed
            : DistributedSecurityRateLimitDecision.Rejected(TimeSpan.FromMilliseconds(Math.Max(0, (long)values[1])));
    }
}
