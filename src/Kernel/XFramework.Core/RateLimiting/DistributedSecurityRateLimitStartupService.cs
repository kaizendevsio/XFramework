using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace XFramework.Core.RateLimiting;

internal sealed class DistributedSecurityRateLimitStartupService(IConnectionMultiplexer redis) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            throw new InvalidOperationException(
                "The distributed security rate-limit store is unavailable.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
