using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notifications.Api.Services.Push;

/// <summary>Application server keys for RFC 8292 VAPID. Key material is never persisted by this code.</summary>
public sealed record WebPushVapidKeys(string PublicKey, byte[] PublicKeyBytes, byte[] PrivateKeyBytes, string Subject);

/// <summary>
/// Resolves VAPID keys for a tenant: the per-tenant NotificationProviderSetting row wins, and
/// configuration (secret store / environment) is the deployment-wide fallback. When neither is
/// present push disables itself quietly instead of failing every delivery attempt loudly.
/// </summary>
public sealed class WebPushVapidProvider(
    AppDbContext db,
    IConfiguration configuration,
    ILogger<WebPushVapidProvider> logger)
{
    public const string ProviderKey = "web-push";

    // One warning per process: an unconfigured deployment must not flood logs on every poll.
    private static int missingLogged;

    private readonly Dictionary<Guid, WebPushVapidKeys?> cache = [];

    public async ValueTask<WebPushVapidKeys?> GetAsync(Guid tenantId, CancellationToken ct)
    {
        if (cache.TryGetValue(tenantId, out var cached))
            return cached;

        var resolved = await ResolveAsync(tenantId, ct);
        cache[tenantId] = resolved;

        if (resolved is null && Interlocked.Exchange(ref missingLogged, 1) == 0)
        {
            logger.LogWarning(
                "Web push is disabled: no VAPID key pair is configured. Set Notifications:Push:Vapid:PublicKey, " +
                "Notifications:Push:Vapid:PrivateKey and Notifications:Push:Vapid:Subject, or add a Push " +
                "NotificationProviderSetting row for the tenant.");
        }

        return resolved;
    }

    private async ValueTask<WebPushVapidKeys?> ResolveAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId != Guid.Empty)
        {
            var setting = await db.Set<NotificationProviderSetting>()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Channel == NotificationDeliveryChannel.Push)
                .Where(x => !x.IsDeleted && x.IsEnabled)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.CreatedAt)
                .Select(x => x.SettingsJson)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(setting))
            {
                VapidProviderSettings? parsed = null;
                try
                {
                    parsed = JsonSerializer.Deserialize<VapidProviderSettings>(setting);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Push provider settings for tenant {TenantId} are not valid JSON", tenantId);
                }

                if (TryBuild(parsed?.PublicKey, parsed?.PrivateKey, parsed?.Subject, out var tenantKeys))
                    return tenantKeys;
            }
        }

        return TryReadConfiguration(out var keys) ? keys : null;
    }

    private bool TryReadConfiguration(out WebPushVapidKeys? keys) =>
        TryBuild(
            configuration["Notifications:Push:Vapid:PublicKey"],
            configuration["Notifications:Push:Vapid:PrivateKey"],
            configuration["Notifications:Push:Vapid:Subject"],
            out keys);

    private bool TryBuild(string? publicKey, string? privateKey, string? subject, out WebPushVapidKeys? keys)
    {
        keys = null;
        if (!PushBase64Url.TryDecode(publicKey, 65, out var publicBytes) ||
            !PushBase64Url.TryDecode(privateKey, 32, out var privateBytes))
        {
            return false;
        }

        if (publicBytes[0] != 0x04)
            return false;

        // RFC 8292 requires a contactable subject so a push service can reach the operator.
        var contact = string.IsNullOrWhiteSpace(subject) ? "mailto:push@localhost" : subject.Trim();
        keys = new WebPushVapidKeys(PushBase64Url.Encode(publicBytes), publicBytes, privateBytes, contact);
        return true;
    }

    private sealed record VapidProviderSettings(
        [property: JsonPropertyName("publicKey")] string? PublicKey,
        [property: JsonPropertyName("privateKey")] string? PrivateKey,
        [property: JsonPropertyName("subject")] string? Subject);
}
