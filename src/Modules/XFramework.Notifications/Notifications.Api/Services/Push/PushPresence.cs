using Microsoft.Extensions.Caching.Memory;

namespace Notifications.Api.Services.Push;

/// <summary>Short foreground leases for the current single-instance Notifications service.
/// Restarting or losing a heartbeat fails open: the device receives push again.</summary>
public sealed class PushPresence(IMemoryCache cache)
{
    private static string Key(Guid tenant, Guid credential, string endpointHash) =>
        $"notifications:tenant:{tenant}:push-presence:{credential}:{endpointHash}";
    private readonly object gate = new();

    public void Set(Guid tenant, Guid credential, string endpointHash, Guid window, bool visible, DateTimeOffset now)
    {
        lock (gate)
        {
            var key = Key(tenant, credential, endpointHash);
            var windows = cache.Get<Dictionary<Guid, DateTimeOffset>>(key) ?? [];
            foreach (var expired in windows.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) windows.Remove(expired);
            if (visible) windows[window] = now.AddSeconds(45);
            else windows.Remove(window);
            // Bound browser sessions as well as the lifetime of a crashed window.
            foreach (var extra in windows.OrderByDescending(x => x.Value).Skip(8).Select(x => x.Key).ToArray()) windows.Remove(extra);
            if (windows.Count == 0) cache.Remove(key);
            else cache.Set(key, windows, TimeSpan.FromSeconds(45));
        }
    }

    public bool IsVisible(Guid tenant, Guid credential, string endpointHash, DateTimeOffset now)
    {
        lock (gate) return cache.Get<Dictionary<Guid, DateTimeOffset>>(Key(tenant, credential, endpointHash))
            ?.Values.Any(expiry => expiry > now) == true;
    }
}
