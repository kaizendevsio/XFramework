using System.Security.Claims;
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
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var actor = await new FixedActor(services.GetRequiredService<YapSessions>(), user).GetCurrentActorAsync(ct)
            ?? throw new YapApiException(401, "Sign in again to continue.");
        using var token = services.GetRequiredService<IActorAccessTokenScope>().Push(actor.AccessToken!);
        var credential = credentialId ?? actor.CredentialId;
        var directory = await services.GetRequiredService<IIdentityServerServiceWrapper>().GetEncryptionDirectory(new GetEncryptionDirectoryRequest
        { CredentialId = credential, Metadata = new RequestMetadata { RequestedTenantId = actor.TenantId, RequestId = Guid.NewGuid() } }, ct);
        if (!directory.IsSuccess || directory.Response is not { } value || value.TenantId != actor.TenantId || value.CredentialId != credential ||
            !value.Devices.Any(x => x.DeviceId == deviceId && x.Revocation is null))
            throw new YapApiException(403, "This calling device is no longer approved.");
        // Signature proof remains on the receiving clients; the relay enforces account ownership and revocation.
    }
}
