using IdentityServer.Domain.Shared.Contracts;
using POS.Api.Features.Reports.GetProductSalesReport;
using XFramework.Core.Patterns;

namespace POS.Api.Services;

public sealed partial class PosReportingService
{
    public async Task<Result<PosProductSalesReportResponse>> GetProductSalesReportAsync(
        GetPosProductSalesReportRequest request, CancellationToken ct)
    {
        if (trustedContext.Current?.Actor is not { } actor || actor.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return Result<PosProductSalesReportResponse>.Unauthorized("Sign in to view POS reports.");
        if (!actor.Capabilities.Contains(PosAuthorizationCapabilities.ReportingView))
            return Result<PosProductSalesReportResponse>.Forbidden("POS Reporting view permission is required.");
        var context = contextResolver.Resolve(request);
        if (!context.IsSuccess)
            return Result<PosProductSalesReportResponse>.Failure(context.Message!, context.StatusCode);
        var tenantId = context.Data!.TenantId;
        if (actor.TenantId != tenantId && !actor.Capabilities.Contains(XFrameworkActorCapabilities.IdentityTenantsManage))
            return Result<PosProductSalesReportResponse>.Forbidden("Managed tenant access is required for this report.");
        var enabled = await features.EnsureEnabledAsync(tenantId, TenantModuleFeatureKeys.Pos, TenantModuleFeatureKeys.PosReportingSubFeature, ct);
        if (!enabled.IsSuccess)
            return Result<PosProductSalesReportResponse>.Failure(enabled.Message!, enabled.StatusCode);
        var validation = new GetPosProductSalesReportValidator().Validate(request);
        if (!validation.IsValid)
            return Result<PosProductSalesReportResponse>.Failure(validation.Errors[0].ErrorMessage, 400);

        logger.LogDebug("Loading POS product sales for tenant {TenantId} from {FromUtc} to {ToUtcExclusive}", tenantId, request.FromUtc, request.ToUtcExclusive);
        var asOf = DateTime.UtcNow;
        var sales = db.Set<PosSale>().AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted &&
            x.Status == PosSaleStatus.Completed && (x.CompletedAt ?? x.CreatedAt) >= request.FromUtc &&
            (x.CompletedAt ?? x.CreatedAt) < request.ToUtcExclusive && (x.CompletedAt ?? x.CreatedAt) <= asOf);
        var lines = from line in db.Set<PosSaleLine>().AsNoTracking()
                    join sale in sales on line.SaleId equals sale.Id
                    where line.TenantId == tenantId && !line.IsDeleted
                    select new { line.ProductId, line.ProductVariationId, sale.CurrencyId,
                        line.ProductName, line.VariantName, line.SKU, line.Quantity,
                        Revenue = line.Quantity * line.UnitPrice - line.DiscountAmount };
        // Group before paging: quantities and amounts include every matching sale line.
        var products = lines.GroupBy(x => new { x.ProductId, x.ProductVariationId, x.CurrencyId })
            .Select(g => new { g.Key.ProductId, g.Key.ProductVariationId, g.Key.CurrencyId,
                ProductName = g.Max(x => x.ProductName), VariantName = g.Max(x => x.VariantName), SKU = g.Max(x => x.SKU),
                Quantity = g.Sum(x => x.Quantity), SalesAmount = g.Sum(x => x.Revenue) });
        var filter = request.ProductFilter?.Trim() ?? "";
        if (filter.Length > 0 || request.ProductFilterOperator is "IsEmpty" or "IsNotEmpty")
            products = request.ProductFilterOperator switch
            {
                "Equals" => products.Where(x => x.ProductName == filter),
                "NotEquals" => products.Where(x => x.ProductName != filter),
                "NotContains" => products.Where(x => !x.ProductName!.Contains(filter)),
                "StartsWith" => products.Where(x => x.ProductName!.StartsWith(filter)),
                "EndsWith" => products.Where(x => x.ProductName!.EndsWith(filter)),
                "IsEmpty" => products.Where(x => x.ProductName == null || x.ProductName == ""),
                "IsNotEmpty" => products.Where(x => x.ProductName != null && x.ProductName != ""),
                _ => products.Where(x => x.ProductName!.Contains(filter))
            };
        var count = await products.CountAsync(ct);
        var rows = await products.OrderBy(x => x.ProductName).ThenBy(x => x.ProductId)
            .ThenBy(x => x.ProductVariationId).ThenBy(x => x.CurrencyId)
            .Skip(request.Skip).Take(request.Take)
            .Select(x => new PosProductSalesRow(x.ProductId, x.ProductVariationId, x.CurrencyId,
                x.ProductName ?? "", x.VariantName, x.SKU, x.Quantity, x.SalesAmount)).ToListAsync(ct);
        var currencies = rows.Select(x => x.CurrencyId).Distinct().ToArray();
        var labelRows = await db.Set<Wallets.Domain.Shared.Contracts.CurrencyType>().AsNoTracking()
            .Where(x => currencies.Contains(x.Id) && !x.IsDeleted && (x.TenantId == tenantId || x.TenantId == Guid.Empty))
            .OrderBy(x => x.Id).Take(request.Take)
            .Select(x => new { x.Id, x.CurrencyIsoCode3, x.Name }).ToListAsync(ct);
        var labels = labelRows.Select(x => new PosReportCurrencyLabel(x.Id,
                !string.IsNullOrWhiteSpace(x.CurrencyIsoCode3) ? x.CurrencyIsoCode3.Trim() : x.Name?.Trim() ?? ""))
            .Where(x => x.Label.Length > 0).ToList();
        return Result<PosProductSalesReportResponse>.Success(new(tenantId, request.FromUtc, request.ToUtcExclusive, count, rows, labels));
    }
}
