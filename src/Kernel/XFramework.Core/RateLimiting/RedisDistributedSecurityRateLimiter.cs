using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace XFramework.Core.RateLimiting;

internal sealed class RedisDistributedSecurityRateLimiter(
    IConnectionMultiplexer redis,
    IOptions<DistributedSecurityRateLimitOptions> options)
    : IDistributedSecurityRateLimiter
{
    private const string FixedWindowScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        local ttl = redis.call('PTTL', KEYS[1])
        return { count, ttl }
        """;

    private readonly DistributedSecurityRateLimitOptions _options = options.Value;

    public async ValueTask<DistributedSecurityRateLimitDecision> AcquireAsync(
        StrictSecurityRateLimitPolicy policy,
        string clientKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = $"{_options.KeyPrefix}:{policy.Name}:{clientKey}";
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            FixedWindowScript,
            [(RedisKey)key],
            [(RedisValue)(long)policy.Window.TotalMilliseconds]).WaitAsync(cancellationToken);
        var values = (RedisResult[]?)result
            ?? throw new RedisServerException("Distributed rate limiter returned an invalid response.");

        if (values.Length != 2)
            throw new RedisServerException("Distributed rate limiter returned an invalid response.");

        var count = (long)values[0];
        var ttlMilliseconds = Math.Max(0, (long)values[1]);
        return count <= policy.PermitLimit
            ? DistributedSecurityRateLimitDecision.Allowed
            : DistributedSecurityRateLimitDecision.Rejected(TimeSpan.FromMilliseconds(ttlMilliseconds));
    }
}
