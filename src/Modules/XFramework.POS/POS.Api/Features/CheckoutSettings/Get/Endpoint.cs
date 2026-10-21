using IdentityServer.Domain.Shared.Contracts;
using POS.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace POS.Api.Features.CheckoutSettings.Get;

public static class GetPosCheckoutSettingsEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView])]
    [MapGet("/api/pos/checkout-settings", Tags = ["POS Sales"], Summary = "Get tenant checkout settings",
        Capability = IdentityAuthorizationConstants.View,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView])]
    public static Task<Result<PosCheckoutSettingsResponse>> Handle(GetPosCheckoutSettingsRequest request,
        PosCheckoutSettingsService service, CancellationToken ct) => service.GetAsync(request, ct);
}
