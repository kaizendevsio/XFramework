using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

namespace Inventario.Api.Features.Reports.LowStockCount;

public static class LowStockCountEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["inventario.reporting:view"],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/inventario/reports/low-stock-count", Tags = ["Inventario Reports"],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["inventario.reporting:view"],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    public static Task<Result<int>> Handle(GetLowStockCountRequest request,
        InventoryReportingService reportingService, CancellationToken ct) => reportingService.GetLowStockCountAsync(request, ct);
}
