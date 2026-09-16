using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Notifications.Api.Services.Push;

/// <summary>
/// Owns per-device Web Push subscriptions and the send fan-out to them.
///
/// Privacy: the app is end-to-end encrypted and the server cannot read message bodies, so a push
/// payload carries routing identifiers only - kind, thread, and an opaque reference. The service
/// worker renders a generic notification and the app fills in the detail once it is open and can
/// decrypt locally. Nothing here needs, or is given, plaintext.
/// </summary>
public sealed class NotificationPushService(
    AppDbContext db,
    WebPushVapidProvider vapid,
    WebPushSender sender,
    ILogger<NotificationPushService> logger,
    ITrustedInvocationContextAccessor invocation,
    PushPresence? presence = null)
{
    public const string KindMessage = "message";
    public const string KindCall = "call";

    public async Task<Result> SetPresenceAsync(SetPushPresenceRequest request, CancellationToken ct)
    {
        if (!TryResolveActor(request.TenantId, request.CredentialId, out var tenant, out var credential, out var failure))
            return Result.Failure(failure, 400);
        if (request.WindowId == Guid.Empty || string.IsNullOrWhiteSpace(request.Endpoint) || request.Endpoint.Length > 2048)
            return Result.Failure("A browser window and subscription are required", 400);
        var hash = HashEndpoint(request.Endpoint.Trim());
        if (!await ActiveSubscriptions(tenant, credential).AnyAsync(x => x.EndpointHash == hash, ct))
            return Result.Failure("Subscription not found", 404);
        presence?.Set(tenant, credential, hash, request.WindowId, request.Visible, DateTimeOffset.UtcNow);
        return Result.Success();
    }

    public async Task<Result<PushConfigurationResponse>> GetConfigurationAsync(
        GetPushConfigurationRequest request,
        CancellationToken ct)
    {
        if (!TryResolveActor(request.TenantId, request.CredentialId, out var tenantId, out var credentialId, out var failure))
            return Result<PushConfigurationResponse>.Failure(failure, 400);

        var keys = await vapid.GetAsync(tenantId, ct);
        var count = keys is null
            ? 0
            : await ActiveSubscriptions(tenantId, credentialId).CountAsync(ct);

        return Result<PushConfigurationResponse>.Success(new PushConfigurationResponse
        {
            Enabled = keys is not null,
            VapidPublicKey = keys?.PublicKey,
            SubscriptionCount = count
        });
    }

    public async Task<Result<PushSubscriptionResponse>> RegisterAsync(
        RegisterPushSubscriptionRequest request,
        CancellationToken ct)
    {
        if (!TryResolveActor(request.TenantId, request.CredentialId, out var tenantId, out var credentialId, out var failure))
            return Result<PushSubscriptionResponse>.Failure(failure, 400);

        var endpoint = request.Endpoint?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return Result<PushSubscriptionResponse>.Failure("The push endpoint must be an absolute https URL", 400);

        // Reject unusable key material here rather than discovering it at send time, when the
        // only signal would be a subscription that silently never delivers.
        if (!PushBase64Url.TryDecode(request.P256dh, 65, out var publicKey) || publicKey[0] != 0x04)
            return Result<PushSubscriptionResponse>.Failure("The p256dh key must be a 65-byte uncompressed P-256 point", 400);
        if (!PushBase64Url.TryDecode(request.Auth, 16, out _))
            return Result<PushSubscriptionResponse>.Failure("The auth secret must be 16 bytes", 400);

        var now = DateTime.UtcNow;
        var hash = HashEndpoint(endpoint);
        var existing = await db.Set<NotificationPushSubscription>()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.EndpointHash == hash, ct);

        if (existing is null)
        {
            existing = new NotificationPushSubscription
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EndpointHash = hash,
                Endpoint = endpoint,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            };
            db.Set<NotificationPushSubscription>().Add(existing);
        }

        // A re-installed PWA can reuse an endpoint the push service previously handed to someone
        // else on a shared device; rebinding the credential keeps one row authoritative.
        existing.CredentialId = credentialId;
        existing.P256dh = request.P256dh.Trim();
        existing.Auth = request.Auth.Trim();
        existing.DeviceId = request.DeviceId == Guid.Empty ? null : request.DeviceId;
        existing.DeviceLabel = Truncate(request.DeviceLabel, 64);
        existing.ExpiresAt = request.ExpiresAt;
        existing.LastSeenAt = now;
        existing.ConsecutiveFailureCount = 0;
        existing.LastErrorCode = null;
        existing.IsDeleted = false;
        existing.DeletedAt = null;
        existing.IsEnabled = true;
        existing.ModifiedAt = now;

        await db.SaveChangesAsync(ct);

        return Result<PushSubscriptionResponse>.Success(new PushSubscriptionResponse
        {
            Id = existing.Id,
            TenantId = tenantId,
            CredentialId = credentialId,
            DeviceId = existing.DeviceId,
            LastSeenAt = existing.LastSeenAt
        }, "Push subscription registered");
    }

    public async Task<Result> RemoveAsync(RemovePushSubscriptionRequest request, CancellationToken ct)
    {
        if (!TryResolveActor(request.TenantId, request.CredentialId, out var tenantId, out var credentialId, out var failure))
            return Result.Failure(failure, 400);

        var hash = HashEndpoint(request.Endpoint?.Trim() ?? string.Empty);
        var removed = await db.Set<NotificationPushSubscription>()
            .Where(x => x.TenantId == tenantId && x.CredentialId == credentialId && x.EndpointHash == hash)
            .ExecuteDeleteAsync(ct);

        // Unsubscribing is idempotent: the browser may have dropped the subscription already.
        return Result.Success(removed > 0 ? "Push subscription removed" : "No matching push subscription");
    }

    /// <summary>Sends a routing-only push to every device registered for a credential.</summary>
    public async Task<SendDirectPushResponse> SendAsync(
        Guid tenantId,
        Guid credentialId,
        PushEnvelope envelope,
        int timeToLiveSeconds,
        string urgency,
        CancellationToken ct)
    {
        var summary = new SendDirectPushResponse();
        if (tenantId == Guid.Empty || credentialId == Guid.Empty)
            return summary;

        var keys = await vapid.GetAsync(tenantId, ct);
        if (keys is null)
            return summary;

        var subscriptions = await ActiveSubscriptions(tenantId, credentialId)
            .AsNoTracking()
            .Take(20) // A person with more than twenty live endpoints has stale rows, not twenty phones.
            .ToListAsync(ct);
        if (subscriptions.Count == 0)
            return summary;

        // Stamped here rather than by each caller: this is the only place that knows which
        // credential the push is actually addressed to, and a device with two enrolled accounts
        // cannot otherwise tell which key store to open or which account header to send.
        var payload = JsonSerializer.SerializeToUtf8Bytes(envelope with { Account = $"{tenantId:N}:{credentialId:N}" }, PushEnvelopeJson);
        var now = DateTime.UtcNow;
        var gone = new List<Guid>();
        var delivered = new List<Guid>();
        var failed = new List<(Guid Id, string Code)>();

        foreach (var subscription in subscriptions)
        {
            if (presence?.IsVisible(tenantId, credentialId, subscription.EndpointHash, DateTimeOffset.UtcNow) == true)
            {
                summary.Suppressed++;
                continue;
            }
            var result = await sender.SendAsync(
                keys,
                subscription.Endpoint,
                subscription.P256dh,
                subscription.Auth,
                payload,
                timeToLiveSeconds,
                urgency,
                ct);

            switch (result.Outcome)
            {
                case WebPushOutcome.Delivered:
                    delivered.Add(subscription.Id);
                    break;
                // 404/410 is the push service saying this endpoint will never work again.
                // Retrying it forever is how push tables rot, so the row goes now.
                case WebPushOutcome.Gone:
                    gone.Add(subscription.Id);
                    break;
                default:
                    failed.Add((subscription.Id, result.Outcome.ToString().ToLowerInvariant()));
                    logger.LogDebug(
                        "Web push to subscription {SubscriptionId} failed with {Outcome} ({Status}): {Detail}",
                        subscription.Id,
                        result.Outcome,
                        result.StatusCode,
                        result.Detail);
                    break;
            }
        }

        if (gone.Count > 0)
        {
            await db.Set<NotificationPushSubscription>()
                .Where(x => gone.Contains(x.Id))
                .ExecuteDeleteAsync(ct);
        }

        if (delivered.Count > 0)
        {
            await db.Set<NotificationPushSubscription>()
                .Where(x => delivered.Contains(x.Id))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.LastSuccessAt, now)
                    .SetProperty(x => x.LastSeenAt, now)
                    .SetProperty(x => x.ConsecutiveFailureCount, 0)
                    .SetProperty(x => x.LastErrorCode, (string?)null)
                    .SetProperty(x => x.ModifiedAt, now), ct);
        }

        foreach (var group in failed.GroupBy(x => x.Code))
        {
            var ids = group.Select(x => x.Id).ToList();
            await db.Set<NotificationPushSubscription>()
                .Where(x => ids.Contains(x.Id))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.LastFailureAt, now)
                    .SetProperty(x => x.ConsecutiveFailureCount, x => x.ConsecutiveFailureCount + 1)
                    .SetProperty(x => x.LastErrorCode, group.Key)
                    .SetProperty(x => x.ModifiedAt, now), ct);
        }

        summary.Delivered = delivered.Count;
        summary.Removed = gone.Count;
        summary.Failed = failed.Count;
        return summary;
    }

    public async Task<Result<SendDirectPushResponse>> SendDirectAsync(SendDirectPushRequest request, CancellationToken ct)
    {
        var tenantId = invocation.Current?.EffectiveTenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return Result<SendDirectPushResponse>.Failure("Tenant ID is required", 400);
        if (request.RecipientCredentialId == Guid.Empty)
            return Result<SendDirectPushResponse>.Failure("Recipient credential ID is required", 400);

        // Only the two kinds the service worker knows how to render; anything else would produce
        // a notification the device cannot describe.
        var kind = string.Equals(request.Kind?.Trim(), KindCall, StringComparison.OrdinalIgnoreCase) ? KindCall : KindMessage;
        var summary = await SendAsync(
            tenantId,
            request.RecipientCredentialId,
            new PushEnvelope(1, kind, request.ThreadId, null, Truncate(request.Reference, 64), request.ExpiresAt?.ToUnixTimeSeconds()),
            request.TimeToLiveSeconds,
            NormalizeUrgency(request.Urgency),
            ct);

        return Result<SendDirectPushResponse>.Success(summary);
    }

    /// <summary>True when the credential has at least one device that can receive push.</summary>
    public Task<bool> HasSubscriptionsAsync(Guid tenantId, Guid credentialId, CancellationToken ct) =>
        ActiveSubscriptions(tenantId, credentialId).AnyAsync(ct);

    /// <summary>Drops subscriptions the push service gave an expiry for that has since passed.</summary>
    public Task<int> PruneExpiredAsync(CancellationToken ct) =>
        db.Set<NotificationPushSubscription>()
            .Where(x => x.ExpiresAt != null && x.ExpiresAt <= DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);

    internal static string HashEndpoint(string endpoint) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint))).ToLowerInvariant();

    private IQueryable<NotificationPushSubscription> ActiveSubscriptions(Guid tenantId, Guid credentialId) =>
        db.Set<NotificationPushSubscription>()
            .Where(x => x.TenantId == tenantId && x.CredentialId == credentialId)
            .Where(x => !x.IsDeleted && x.IsEnabled);

    private static string NormalizeUrgency(string? urgency) =>
        urgency?.Trim().ToLowerInvariant() switch
        {
            "very-low" => "very-low",
            "low" => "low",
            "high" => "high",
            _ => "normal"
        };

    private static string? Truncate(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= length ? value.Trim() : value.Trim()[..length];

    // Binds every subscription operation to the validated actor. A client-supplied credential ID is
    // only ever accepted when it matches, because registering someone else's device would leak the
    // fact and timing of their messages.
    private bool TryResolveActor(
        Guid? requestTenantId,
        Guid requestCredentialId,
        out Guid tenantId,
        out Guid credentialId,
        out string failure)
    {
        tenantId = Guid.Empty;
        credentialId = Guid.Empty;
        failure = "Tenant ID is required";

        var context = invocation.Current;
        var effectiveTenantId = context?.EffectiveTenantId ?? Guid.Empty;
        if (effectiveTenantId == Guid.Empty)
            return false;
        if (requestTenantId is { } supplied && supplied != Guid.Empty && supplied != effectiveTenantId)
            return false;

        var actor = context?.Actor?.CredentialId ?? Guid.Empty;
        if (actor == Guid.Empty)
        {
            failure = "An actor identity is required for push subscriptions";
            return false;
        }

        if (requestCredentialId != Guid.Empty && requestCredentialId != actor)
        {
            failure = "Push subscriptions can only be managed for the signed-in credential";
            return false;
        }

        tenantId = effectiveTenantId;
        credentialId = actor;
        return true;
    }

    internal static readonly JsonSerializerOptions PushEnvelopeJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// The complete push payload. Deliberately routing-only: <paramref name="Kind"/> tells the service
/// worker which generic string to render, and the identifiers let a tap open the right conversation.
/// No title, body, sender name or preview is ever included - the server cannot read them anyway.
/// </summary>
/// <param name="Version">Payload version so an old installed service worker can ignore what it cannot read.</param>
/// <param name="Kind">"message" or "call".</param>
/// <param name="ThreadId">Conversation to open on tap.</param>
/// <param name="NotificationId">Inbox item, so the app can mark it read without refetching everything.</param>
/// <param name="Reference">Opaque collapse key, for example a call ID.</param>
/// <param name="ExpiresAt">
/// Unix seconds after which the event is stale, for a ringing invite the moment it times out.
/// Unix seconds rather than a timestamp string because the worker only ever compares it to
/// Date.now(), and a push payload has four kilobytes to spend. Null for events that do not expire.
/// </param>
/// <param name="Account">
/// "tenantId:credentialId", both compact, stamped by <see cref="NotificationPushService.SendAsync"/>.
/// One device can hold several enrolled accounts, and a module service worker that wants to decrypt
/// this conversation locally has to know whose key store to open and which X-Yap-Account header to
/// send. Still routing only: two identifiers the receiving device already stores, and nothing the
/// recipient does not already know about themselves.
/// </param>
public sealed record PushEnvelope(
    int Version,
    string Kind,
    Guid? ThreadId,
    Guid? NotificationId,
    string? Reference,
    long? ExpiresAt = null,
    string? Account = null);
