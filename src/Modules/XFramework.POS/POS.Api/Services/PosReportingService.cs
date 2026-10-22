using IdentityServer.Domain.Shared.Contracts;
using POS.Api.Features.Reports.GetSalesReport;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;

namespace POS.Api.Services;

public sealed partial class PosReportingService(
    AppDbContext db,
    IPosRequestContextResolver contextResolver,
    ITrustedInvocationContextAccessor trustedContext,
    ITenantModuleFeatureService features,
    ILogger<PosReportingService> logger)
{
    public const int MaxCurrencies = 16;

    public async Task<Result<PosSalesReportResponse>> GetSalesReportAsync(
        GetPosSalesReportRequest request, CancellationToken ct)
    {
        if (trustedContext.Current?.Actor is not { } actor || actor.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return Result<PosSalesReportResponse>.Unauthorized("Sign in to view POS reports.");
        if (!actor.Capabilities.Contains(PosAuthorizationCapabilities.ReportingView))
            return Result<PosSalesReportResponse>.Forbidden("POS Reporting view permission is required.");
        var context = contextResolver.Resolve(request);
        if (!context.IsSuccess)
            return Result<PosSalesReportResponse>.Failure(context.Message!, context.StatusCode);
        var tenantId = context.Data!.TenantId;
        if (actor.TenantId != tenantId && !actor.Capabilities.Contains(XFrameworkActorCapabilities.IdentityTenantsManage))
            return Result<PosSalesReportResponse>.Forbidden("Managed tenant access is required for this report.");
        var enabled = await features.EnsureEnabledAsync(tenantId, TenantModuleFeatureKeys.Pos, TenantModuleFeatureKeys.PosReportingSubFeature, ct);
        if (!enabled.IsSuccess)
            return Result<PosSalesReportResponse>.Failure(enabled.Message!, enabled.StatusCode);
        var validation = new GetPosSalesReportValidator().Validate(request);
        if (!validation.IsValid)
            return Result<PosSalesReportResponse>.Failure(validation.Errors[0].ErrorMessage, 400);

        logger.LogDebug("Loading POS sales report for tenant {TenantId}", tenantId);
        var asOf = DateTime.UtcNow;
        var from = request.HourUtc is { } hour ? request.FromUtc.AddHours(hour) : request.FromUtc;
        var to = request.HourUtc.HasValue ? from.AddHours(1) : request.ToUtcExclusive;
        var sales = db.Set<PosSale>().AsNoTracking().Where(x => x.TenantId == tenantId &&
            (x.CompletedAt ?? x.CreatedAt) >= from && (x.CompletedAt ?? x.CreatedAt) < to && (x.CompletedAt ?? x.CreatedAt) <= asOf);
        if (request.RegisterId is { } registerId) sales = sales.Where(x => x.RegisterId == registerId);
        if (request.CurrencyId is { } currencyId) sales = sales.Where(x => x.CurrencyId == currencyId);
        if (request.Status is { } status) sales = sales.Where(x => x.Status == status);
        if (request.PaymentMethod is { } method) sales = sales.Where(x => x.PaymentMethod == method);

        // Refund recognition is independent of the original sale's date. CompletedAt is set when the refund succeeds.
        var refunds = db.Set<PosReturn>().AsNoTracking().Where(x => x.TenantId == tenantId &&
            x.Status == PosReturnStatus.Completed && x.CompletedAt >= from && x.CompletedAt < to && x.CompletedAt <= asOf);
        if (request.RegisterId is { } refundRegister) refunds = refunds.Where(x => x.RegisterId == refundRegister);
        if (request.CurrencyId is { } refundCurrency) refunds = refunds.Where(x => x.CurrencyId == refundCurrency);
        if (request.PaymentMethod is { } refundMethod) refunds = refunds.Where(x => x.RefundMethod == refundMethod);
        if (request.Status is { } saleStatus)
            refunds = refunds.Where(x => db.Set<PosSale>().Any(s => s.TenantId == tenantId && s.Id == x.SaleId && s.Status == saleStatus));

        // All aggregates execute in PostgreSQL. No receipt entities or line graphs are materialized.
        var completed = sales.Where(x => x.Status == PosSaleStatus.Completed);
        var currencies = await completed.GroupBy(x => x.CurrencyId)
            .OrderBy(g => g.Key)
            .Select(g => new PosReportCurrencySummary(g.Key, g.Count(),
                g.Sum(x => x.SubtotalAmount), g.Sum(x => x.DiscountAmount), g.Sum(x => x.TaxAmount),
                g.Sum(x => x.TotalAmount),
                g.Sum(x => x.PaymentMethod == PosPaymentMethod.CashDrawer ? x.TotalAmount : 0),
                g.Sum(x => x.PaymentMethod == PosPaymentMethod.WalletTransfer ? x.TotalAmount : 0)))
            .Take(MaxCurrencies + 1).ToListAsync(ct);
        var refundTotals = await refunds.GroupBy(x => x.CurrencyId).OrderBy(g => g.Key)
            .Select(g => new { CurrencyId = g.Key, Count = g.Count(), Total = g.Sum(x => x.TotalRefundAmount) })
            .Take(MaxCurrencies + 1).ToListAsync(ct);
        var currencyIds = currencies.Select(x => x.CurrencyId).Union(refundTotals.Select(x => x.CurrencyId)).ToList();
        if (currencyIds.Count > MaxCurrencies)
            return Result<PosSalesReportResponse>.Failure("Select a currency for reports with more than 16 currencies.", 400);
        currencies = currencyIds.OrderBy(x => x).Select(id =>
        {
            var gross = currencies.FirstOrDefault(x => x.CurrencyId == id) ?? new(id, 0, 0, 0, 0, 0, 0, 0);
            var refund = refundTotals.FirstOrDefault(x => x.CurrencyId == id);
            return gross with { RefundCount = refund?.Count ?? 0, RefundTotal = refund?.Total ?? 0 };
        }).ToList();

        var hourly = request.ToUtcExclusive - request.FromUtc == TimeSpan.FromDays(1);
        var timeline = await completed.GroupBy(x => new { x.CurrencyId, Date = (x.CompletedAt ?? x.CreatedAt).Date, Hour = hourly ? (x.CompletedAt ?? x.CreatedAt).Hour : 0 })
            .OrderBy(g => g.Key.Date).ThenBy(g => g.Key.Hour).ThenBy(g => g.Key.CurrencyId)
            .Select(g => new PosReportDay(g.Key.CurrencyId, g.Key.Date.AddHours(g.Key.Hour), g.Count(), g.Sum(x => x.TotalAmount)))
            .Take(366 * MaxCurrencies).ToListAsync(ct);
        var refundTimeline = await refunds.GroupBy(x => new { x.CurrencyId, Date = x.CompletedAt!.Value.Date, Hour = hourly ? x.CompletedAt.Value.Hour : 0 })
            .OrderBy(g => g.Key.Date).ThenBy(g => g.Key.Hour).ThenBy(g => g.Key.CurrencyId)
            .Select(g => new PosReportDay(g.Key.CurrencyId, g.Key.Date.AddHours(g.Key.Hour), 0, 0)
                { RefundTotal = g.Sum(x => x.TotalRefundAmount) })
            .Take(366 * MaxCurrencies).ToListAsync(ct);
        timeline = timeline.Concat(refundTimeline).GroupBy(x => new { x.CurrencyId, x.DateUtc })
            .Select(g => new PosReportDay(g.Key.CurrencyId, g.Key.DateUtc, g.Sum(x => x.Count), g.Sum(x => x.Total))
                { RefundTotal = g.Sum(x => x.RefundTotal) }).OrderBy(x => x.DateUtc).ThenBy(x => x.CurrencyId).ToList();
        var statuses = await sales.GroupBy(x => x.Status).OrderBy(g => g.Key)
            .Select(g => new PosReportStatusCount(g.Key, g.Count()))
            .Take(32).ToListAsync(ct);
        var count = await sales.CountAsync(ct);
        var rows = await sales.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new PosReportSaleRow(x.Id, x.SaleNumber, x.RegisterId,
                db.Set<PosRegister>().Where(r => r.TenantId == tenantId && r.Id == x.RegisterId)
                    .Select(r => r.Name).FirstOrDefault() ?? "Archived register",
                x.CurrencyId, x.Status, x.PaymentMethod, x.TotalAmount, x.CreatedAt, x.CompletedAt))
            .ToListAsync(ct);
        var refundCount = await refunds.CountAsync(ct);
        var refundRows = await refunds.OrderByDescending(x => x.CompletedAt).ThenByDescending(x => x.Id)
            .Skip((request.RefundPage - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new PosReportRefundRow(x.Id, x.ReturnNumber, x.SaleId, x.CurrencyId,
                x.RefundMethod, x.TotalRefundAmount, x.CompletedAt!.Value)).ToListAsync(ct);
        // Report reference labels are a bounded read model, not access to Wallets operations or its currency catalog.
        // Keep normal tenant/global and soft-delete filters; include only IDs present in this authorized snapshot.
        var labelIds = currencyIds.Concat(rows.Select(x => x.CurrencyId)).Concat(refundRows.Select(x => x.CurrencyId)).Distinct().ToList();
        var labelRows = await db.Set<Wallets.Domain.Shared.Contracts.CurrencyType>().AsNoTracking()
            .Where(x => labelIds.Contains(x.Id) && (x.TenantId == tenantId || x.TenantId == Guid.Empty))
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.CurrencyIsoCode3, x.Name })
            .Take(MaxCurrencies + 2 * request.PageSize).ToListAsync(ct);
        var labels = labelRows.Select(x => new PosReportCurrencyLabel(x.Id,
                !string.IsNullOrWhiteSpace(x.CurrencyIsoCode3) ? x.CurrencyIsoCode3.Trim() : x.Name?.Trim() ?? ""))
            .Where(x => x.Label.Length > 0).ToList();
        logger.LogDebug("Loaded POS sales report for tenant {TenantId}: {Count} matching sales", tenantId, count);
        return Result<PosSalesReportResponse>.Success(new(tenantId, asOf, request.FromUtc, request.ToUtcExclusive,
            count, request.Page, request.PageSize, currencies, timeline, statuses, rows)
            { RefundCount = refundCount, RefundPage = request.RefundPage, RefundRows = refundRows, CurrencyLabels = labels });
    }
}
