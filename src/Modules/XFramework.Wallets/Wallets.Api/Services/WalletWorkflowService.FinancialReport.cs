using Wallets.Api.Features.Reports.Financial;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Api.Services;

public sealed partial class WalletWorkflowService
{
    public async Task<Result<WalletFinancialReportResponse>> GetFinancialReportAsync(
        WalletFinancialReportRequest request, CancellationToken ct = default)
    {
        var validation = await new WalletFinancialReportValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<WalletFinancialReportResponse>.Failure(validation.Errors[0].ErrorMessage);
        var context = contextResolver.Resolve(request);
        if (!context.IsSuccess) return Result<WalletFinancialReportResponse>.Failure(context.Message!, context.StatusCode);
        var feature = await EnsureFeatureAsync(context.Data!, TenantModuleFeatureKeys.WalletsReporting, ct);
        if (!feature.IsSuccess) return Failure<WalletFinancialReportResponse>(feature);
        var scope = await ResolveReportWalletScopeAsync(context.Data!, request.WalletId, ct);
        if (!scope.IsSuccess) return Result<WalletFinancialReportResponse>.Failure(scope.Message!, scope.StatusCode);
        var tenantId = context.Data!.TenantId;

        var wallets = dbContext.Set<Wallet>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (request.WalletId.HasValue) wallets = wallets.Where(x => x.Id == request.WalletId.Value);
        if (!scope.Data!.TenantWide) wallets = wallets.Where(x => scope.Data.WalletIds.Contains(x.Id));
        var balances = await wallets.GroupBy(x => x.WalletType != null && !x.WalletType.IsDeleted && x.WalletType.TenantId == tenantId
                ? x.WalletType.CurrencyTypeId : null)
            .Select(group => new {
                CurrencyId = group.Key, WalletCount = group.Count(),
                Balance = group.Sum(x => x.Balance),
                Available = group.Sum(x => x.Balance - x.DebitOnHoldBalance),
                DebitHold = group.Sum(x => x.DebitOnHoldBalance),
                CreditHold = group.Sum(x => x.CreditOnHoldBalance)
            }).Take(65).ToListAsync(ct);

        // Holds and external counter-entries are not posted wallet activity. Fees remain part of debits.
        var ledger = dbContext.Set<WalletLedgerEntry>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.WalletId.HasValue &&
                wallets.Any(wallet => wallet.Id == x.WalletId) &&
                (x.BalanceBucket == WalletBalanceBucket.Available || x.BalanceBucket == WalletBalanceBucket.Fee) &&
                x.EntryKind != WalletLedgerEntryKind.SystemCounterparty &&
                x.CreatedAt >= request.From && x.CreatedAt < request.ToExclusive);
        var activity = await ledger.GroupBy(x => x.CurrencyId).Select(group => new {
            CurrencyId = group.Key,
            Credits = group.Sum(x => x.Direction == WalletLedgerDirection.Credit ? x.Amount : 0m),
            Debits = group.Sum(x => x.Direction == WalletLedgerDirection.Debit ? x.Amount : 0m),
            Fees = group.Sum(x => x.EntryKind == WalletLedgerEntryKind.Fee ? x.Amount : 0m),
            Count = group.Count()
        }).Take(65).ToListAsync(ct);
        var currencyIds = balances.Select(x => x.CurrencyId).Union(activity.Select(x => x.CurrencyId)).ToList();
        if (currencyIds.Count > 64)
            return Result<WalletFinancialReportResponse>.Failure("Choose a wallet to report on at most 64 currencies at a time.");
        var daily = await ledger.GroupBy(x => new { x.CurrencyId, Date = x.CreatedAt.Date })
            .Select(group => new WalletFinancialDailyActivity {
                CurrencyId = group.Key.CurrencyId, Date = group.Key.Date,
                Credits = group.Sum(x => x.Direction == WalletLedgerDirection.Credit ? x.Amount : 0m),
                Debits = group.Sum(x => x.Direction == WalletLedgerDirection.Debit ? x.Amount : 0m)
            }).OrderBy(x => x.Date).ThenBy(x => x.CurrencyId).ToListAsync(ct);
        // Keep currency labels behind their normal tenant/global query filters.
        var labels = await dbContext.Set<Wallets.Domain.Shared.Contracts.CurrencyType>().AsNoTracking()
            .Where(x => currencyIds.Contains(x.Id)).Select(x => new { x.Id, x.CurrencyIsoCode3, x.Name }).ToDictionaryAsync(x => x.Id, ct);
        var summaries = currencyIds.Select(id => {
            var balance = balances.FirstOrDefault(x => x.CurrencyId == id);
            var flow = activity.FirstOrDefault(x => x.CurrencyId == id);
            var label = id.HasValue && labels.TryGetValue(id.Value, out var currency)
                ? currency.CurrencyIsoCode3 ?? currency.Name ?? id.Value.ToString() : id?.ToString() ?? "Unspecified";
            return new WalletFinancialCurrencySummary {
                CurrencyId = id, Currency = label, WalletCount = balance?.WalletCount ?? 0,
                Balance = balance?.Balance ?? 0, AvailableBalance = balance?.Available ?? 0,
                DebitHold = balance?.DebitHold ?? 0, CreditHold = balance?.CreditHold ?? 0,
                Credits = flow?.Credits ?? 0, Debits = flow?.Debits ?? 0, Fees = flow?.Fees ?? 0, EntryCount = flow?.Count ?? 0
            };
        }).OrderBy(x => x.Currency).ThenBy(x => x.CurrencyId).ToList();
        return Result<WalletFinancialReportResponse>.Success(new() {
            TenantId = tenantId, GeneratedAt = DateTime.UtcNow, Currencies = summaries, DailyActivity = daily
        });
    }
}
