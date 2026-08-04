using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Portal.Services;

public sealed class PortalBootstrapSeeder(
    IIdentityServerServiceWrapper identityServer,
    ILogger<PortalBootstrapSeeder> logger)
{
    public async Task SeedAsync(PortalAuthOptions options, CancellationToken ct)
    {
        var result = await identityServer.EnsurePortalBootstrapAdmin(
            new EnsurePortalBootstrapAdminRequest
            {
                TenantName = options.TenantName,
                DisplayName = options.DisplayName,
                UserName = options.UserName,
                Password = options.Password!,
                Metadata = new RequestMetadata
                {
                    OperationName = "Portal",
                    RequestId = Guid.NewGuid(),
                    RequestedTenantId = PortalBootstrapConstants.AdminTenantId
                }
            }).WaitAsync(ct);

        if (!result.IsSuccess || result.Response is null)
        {
            logger.LogError(
                "Portal bootstrap administrator could not be ensured. Status={StatusCode}",
                (int)result.HttpStatusCode);
            throw new InvalidOperationException("Portal bootstrap administrator could not be ensured.");
        }

        logger.LogInformation(
            "Portal bootstrap administrator ensured for tenant {TenantId} and credential {CredentialId}.",
            result.Response.TenantId,
            result.Response.CredentialId);
    }
}
