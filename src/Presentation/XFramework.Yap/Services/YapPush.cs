using Communications.Integration.Clients;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Yap.Services;

/// <summary>
/// Browser-facing wrapper over the Notifications module's push subscription store.
///
/// Yap keeps no database of its own, so the subscription rows live in the Notifications schema
/// that owns every other delivery channel. These endpoints forward the signed-in actor's token so
/// the module binds each subscription to the real credential rather than a client-supplied ID.
/// </summary>
public static class YapPush
{
    public static void MapYapPush(this RouteGroupBuilder api)
    {
        api.MapPost("/push/presence", async (PushPresenceRequest request, INotificationsServiceWrapper notifications,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            var result = await notifications.SetPushPresence(new SetPushPresenceRequest
            {
                CredentialId = actor.CredentialId, Endpoint = request.Endpoint ?? string.Empty,
                WindowId = request.WindowId, Visible = request.Visible, Metadata = Metadata(actor.TenantId)
            }, ct);
            return Results.StatusCode((int)result.HttpStatusCode);
        });
        // The public application server key is safe to hand out; it is what PushManager.subscribe
        // needs. Reporting enabled:false lets the client hide the toggle instead of failing later.
        api.MapGet("/push/config", async (INotificationsServiceWrapper notifications,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            var result = await notifications.GetPushConfiguration(new GetPushConfigurationRequest
            { CredentialId = actor.CredentialId, Metadata = Metadata(actor.TenantId) }, ct);

            // A Notifications outage must not break the settings page; push simply reads as off.
            return result is { IsSuccess: true, Response: not null }
                ? new PushConfig(result.Response.Enabled, result.Response.VapidPublicKey, result.Response.SubscriptionCount)
                : new PushConfig(false, null, 0);
        });

        api.MapPost("/push/subscribe", async (PushSubscribeRequest request, INotificationsServiceWrapper notifications,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            var result = await notifications.RegisterPushSubscription(new RegisterPushSubscriptionRequest
            {
                CredentialId = actor.CredentialId,
                Endpoint = request.Endpoint ?? string.Empty,
                P256dh = request.P256dh ?? string.Empty,
                Auth = request.Auth ?? string.Empty,
                DeviceId = request.DeviceId,
                DeviceLabel = request.Label,
                ExpiresAt = request.ExpiresAt,
                Metadata = Metadata(actor.TenantId)
            }, ct);

            if (!result.IsSuccess)
                throw new YapApiException((int)result.HttpStatusCode, "This device could not be registered for notifications.");
            return Results.NoContent();
        });

        api.MapPost("/push/unsubscribe", async (PushUnsubscribeRequest request, INotificationsServiceWrapper notifications,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            await notifications.RemovePushSubscription(new RemovePushSubscriptionRequest
            { CredentialId = actor.CredentialId, Endpoint = request.Endpoint ?? string.Empty, Metadata = Metadata(actor.TenantId) }, ct);
            // Unsubscribing is best-effort: the browser has already dropped its side either way.
            return Results.NoContent();
        });
    }

    internal static RequestMetadata Metadata(Guid tenant) => new() { RequestedTenantId = tenant, RequestId = Guid.NewGuid() };
}

public sealed record PushConfig(bool Enabled, string? PublicKey, int Devices);
public sealed record PushSubscribeRequest(string? Endpoint, string? P256dh, string? Auth, Guid? DeviceId, string? Label, DateTime? ExpiresAt);
public sealed record PushUnsubscribeRequest(string? Endpoint);
public sealed record PushPresenceRequest(string? Endpoint, Guid WindowId, bool Visible);
