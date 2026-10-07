using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;

namespace Inventario.Api.Features.Setup.UpdatePreferences;

public static class UpdateInventarioPreferencesEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/inventario/setup/preferences", Tags = ["Inventario Setup"], Summary = "Update tenant catalog preferences")]
    public static Task<Result<InventarioSetupResponse>> Handle(UpdateInventarioPreferencesRequest request, InventarioSetupService service, CancellationToken ct) =>
        service.UpdatePreferencesAsync(request, ct);
}
