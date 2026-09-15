using System.Globalization;
using Microsoft.Extensions.Caching.Distributed;

namespace Yap.Services;

// Keep only the latest foreground heartbeat for one day, never a durable activity log.
public sealed class YapPresence(IDistributedCache cache, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan Retention = TimeSpan.FromDays(1);
    private static string Key(Guid tenant, Guid credential) => $"Yap:tenant:{tenant:N}:presence:{credential:N}";

    public async Task TouchAsync(Guid tenant, Guid credential, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            var until = clock.GetUtcNow().Add(Lifetime).ToUnixTimeMilliseconds();
            await cache.SetStringAsync(Key(tenant, credential), until.ToString(CultureInfo.InvariantCulture),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Retention }, timeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* Presence outages must not interrupt messages or produce error toasts. */ }
    }

    public async Task<DateTime?> ActiveUntilAsync(Guid tenant, Guid credential, CancellationToken ct)
    {
        var until = (await LastActiveAtAsync(tenant, credential, ct))?.Add(Lifetime);
        return until > clock.GetUtcNow().UtcDateTime ? until : null;
    }

    public async Task<DateTime?> LastActiveAtAsync(Guid tenant, Guid credential, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            var value = await cache.GetStringAsync(Key(tenant, credential), timeout.Token);
            if (long.TryParse(value, CultureInfo.InvariantCulture, out var milliseconds))
            {
                var last = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).Subtract(Lifetime);
                if (last > clock.GetUtcNow().Subtract(Retention)) return last.UtcDateTime;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* Unavailable presence is displayed as unknown, not online. */ }
        return null;
    }
}
