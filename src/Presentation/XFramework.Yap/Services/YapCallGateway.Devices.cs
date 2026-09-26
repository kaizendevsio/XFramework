using System.Net;
using System.Security.Claims;
using Bolt.Server;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Yap.Services;

public sealed partial class YapCallGateway
{
    private async Task VerifyCallDeviceAsync(ClaimsPrincipal user, Guid deviceId, CancellationToken ct, Guid? credentialId = null)
    {
        if (deviceId == Guid.Empty) throw new YapApiException(403, "An approved calling device is required.");
        if (await CheckCallDeviceAsync(user, deviceId, ct, credentialId) != BoltGroupAuthorizationDecision.Allowed)
            throw new YapApiException(403, "This calling device is no longer approved.");
        // Signature proof remains on the receiving clients; the relay enforces account ownership and revocation.
    }

    /// <summary>
    /// Whether <paramref name="deviceId"/> is still an approved, unrevoked device of this account.
    /// Exceptions from the lookup propagate; <see cref="Classify"/> decides whether they are a refusal.
    /// </summary>
    private async Task<BoltGroupAuthorizationDecision> CheckCallDeviceAsync(ClaimsPrincipal user, Guid deviceId, CancellationToken ct, Guid? credentialId = null)
    {
        if (deviceId == Guid.Empty) return BoltGroupAuthorizationDecision.Denied;
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var actor = await new FixedActor(services.GetRequiredService<YapSessions>(), user).GetCurrentActorAsync(ct)
            ?? throw new YapApiException(401, "Sign in again to continue.");
        using var token = services.GetRequiredService<IActorAccessTokenScope>().Push(actor.AccessToken!);
        var credential = credentialId ?? actor.CredentialId;
        var directory = await services.GetRequiredService<IIdentityServerServiceWrapper>().GetEncryptionDirectory(new GetEncryptionDirectoryRequest
        { CredentialId = credential, Metadata = new RequestMetadata { RequestedTenantId = actor.TenantId, RequestId = Guid.NewGuid() } }, ct);
        if (!directory.IsSuccess || directory.Response is not { } value)
            return IsDefinitiveRefusal(directory.HttpStatusCode) ? BoltGroupAuthorizationDecision.Denied : BoltGroupAuthorizationDecision.Unavailable;
        return value.TenantId == actor.TenantId && value.CredentialId == credential &&
               value.Devices.Any(x => x.DeviceId == deviceId && x.Revocation is null)
            ? BoltGroupAuthorizationDecision.Allowed
            : BoltGroupAuthorizationDecision.Denied;
    }

    /// <summary>
    /// A downstream answer that proves the participant lost access. Everything else - timeouts,
    /// 5xx, an unreachable service, a token that is being rotated (401) - only means the answer is
    /// not available yet, and the relay keeps the seat for its bounded grace period.
    /// </summary>
    private static bool IsDefinitiveRefusal(HttpStatusCode status) =>
        status is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Gone;

    private static BoltGroupAuthorizationDecision Classify(Exception error) => error switch
    {
        // YapSessions raises this only when the sign-in itself is gone or its refresh was refused.
        UnauthorizedAccessException => BoltGroupAuthorizationDecision.Denied,
        YapApiException api when IsDefinitiveRefusal((HttpStatusCode)api.Status) => BoltGroupAuthorizationDecision.Denied,
        _ => BoltGroupAuthorizationDecision.Unavailable
    };
}
