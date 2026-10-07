using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;

namespace Inventario.Api.Features.Setup.Complete;

public static class CompleteInventarioSetupEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/inventario/setup/complete", Tags = ["Inventario Setup"], Summary = "Confirm tenant Inventario setup")]
    public static Task<Result<InventarioSetupResponse>> Handle(CompleteInventarioSetupRequest request, InventarioSetupService service, CancellationToken ct) =>
        service.CompleteAsync(request, ct);
}
