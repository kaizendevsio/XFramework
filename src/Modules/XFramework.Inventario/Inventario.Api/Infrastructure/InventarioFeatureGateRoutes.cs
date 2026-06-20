using IdentityServer.Domain.Shared.Contracts;
using XFramework.Core.Services.FeatureGates;

namespace Inventario.Api.Infrastructure;

public static class InventarioFeatureGateRoutes
{
    public static void Configure(TenantModuleFeatureGateOptions options)
    {
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/warehouses", "warehousing");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/locations", "warehousing");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/stock/balances", "stock_balances");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/stock/movements", "movements");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/stock/post", "movements");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/reservations", "reservations");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/allocations", "reservations");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/lots", "traceability");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/reorder-rules", "planning");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/planning", "planning");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/reports/near-expiry", "traceability");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/reports/expired-stock", "traceability");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/reports", "reporting");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/suppliers", "purchasing");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/purchase-orders", "purchasing");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/receiving", "purchasing");
        options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api");
    }
}
