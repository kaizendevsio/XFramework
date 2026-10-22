using global::Inventario.Api.Features.Transactions.ProductSales;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Inventario.Api.Services;

public sealed class InventoryProductSalesService(DbContext db, ITrustedInvocationContextAccessor trustedContext,
    ITenantModuleFeatureService features, ILogger<InventoryProductSalesService> logger)
{
    public async Task<Result<InventoryProductSalesPage>> GetAsync(GetInventoryProductSalesRequest request, CancellationToken ct)
    {
        var context = trustedContext.Current;
        if (context?.Actor is not { } actor || actor.ExpiresAtUtc <= DateTimeOffset.UtcNow || context.EffectiveTenantId is not { } tenantId || tenantId == Guid.Empty)
            return Result<InventoryProductSalesPage>.Unauthorized("Sign in to view inventory sales.");
        if (!actor.Capabilities.Contains("inventario.transactions:view"))
            return Result<InventoryProductSalesPage>.Forbidden("Inventory transactions view permission is required.");
        if (request.Metadata.RequestedTenantId is { } target && target != tenantId ||
            actor.TenantId != tenantId && !actor.Capabilities.Contains(XFrameworkActorCapabilities.IdentityTenantsManage))
            return Result<InventoryProductSalesPage>.Forbidden("The report tenant is not authorized.");
        var enabled = await features.EnsureEnabledAsync(tenantId, "inventario", "transactions", ct);
        if (!enabled.IsSuccess) return Result<InventoryProductSalesPage>.Failure(enabled.Message!, enabled.StatusCode);
        var validation = new GetInventoryProductSalesValidator().Validate(request);
        if (!validation.IsValid) return Result<InventoryProductSalesPage>.Failure(validation.Errors[0].ErrorMessage, 400);

        logger.LogDebug("Loading direct inventory sales for tenant {TenantId} from {FromUtc} to {ToUtcExclusive}", tenantId, request.FromUtc, request.ToUtcExclusive);
        var totals = db.Set<ProductTransaction>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.TransactionDate >= request.FromUtc && x.TransactionDate < request.ToUtcExclusive)
            .GroupBy(x => new { x.ProductId, x.ProductVariationId })
            .Select(g => new { g.Key.ProductId, g.Key.ProductVariationId, Quantity = g.Sum(x => (decimal)x.Quantity), Revenue = g.Sum(x => x.TotalPrice) });
        var products = from total in totals
                       join product in db.Set<Product>().AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted)
                           on total.ProductId equals product.Id into productGroup
                       from product in productGroup.DefaultIfEmpty()
                       join variant in db.Set<ProductVariation>().AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted)
                           on total.ProductVariationId equals (Guid?)variant.Id into variantGroup
                       from variant in variantGroup.DefaultIfEmpty()
                       select new { total.ProductId, total.ProductVariationId, ProductName = product.Name ?? "Archived product",
                           VariantName = variant.Name, product.SKU, total.Quantity, total.Revenue };
        var filter = request.ProductFilter?.Trim() ?? "";
        if (filter.Length > 0 || request.ProductFilterOperator is "IsEmpty" or "IsNotEmpty")
            products = request.ProductFilterOperator switch
            {
                "Equals" => products.Where(x => x.ProductName == filter),
                "NotEquals" => products.Where(x => x.ProductName != filter),
                "NotContains" => products.Where(x => !x.ProductName.Contains(filter)),
                "StartsWith" => products.Where(x => x.ProductName.StartsWith(filter)),
                "EndsWith" => products.Where(x => x.ProductName.EndsWith(filter)),
                "IsEmpty" => products.Where(x => x.ProductName == ""),
                "IsNotEmpty" => products.Where(x => x.ProductName != ""),
                _ => products.Where(x => x.ProductName.Contains(filter))
            };
        var count = await products.CountAsync(ct);
        var rows = await products.OrderBy(x => x.ProductName).ThenBy(x => x.ProductId).ThenBy(x => x.ProductVariationId)
            .Skip(request.Skip).Take(request.Take)
            .Select(x => new InventoryProductSalesRow(x.ProductId, x.ProductVariationId, x.ProductName,
                x.VariantName, x.SKU, x.Quantity, x.Revenue)).ToListAsync(ct);
        return Result<InventoryProductSalesPage>.Success(new(count, rows));
    }
}
