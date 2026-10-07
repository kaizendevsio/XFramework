using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;

namespace Inventario.Api.Features.Setup.Get;

public static class GetInventarioSetupEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/inventario/setup", Tags = ["Inventario Setup"], Summary = "Get tenant setup status and preferences")]
    public static Task<Result<InventarioSetupResponse>> Handle(GetInventarioSetupRequest request, InventarioSetupService service, CancellationToken ct) =>
        service.GetAsync(request, ct);
}
