using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public sealed partial class WalletWorkflowService
{
    private async Task<Result<Guid>> ResolveOriginalCaseOperationAsync(
        Guid tenantId, Guid walletId, WalletCaseType caseType,
        Guid? transactionId, Guid? operationId, CancellationToken ct)
    {
        if ((!transactionId.HasValue && !operationId.HasValue) ||
            transactionId == Guid.Empty || operationId == Guid.Empty)
            return Result<Guid>.Failure("An original wallet transaction or operation is required", 400);

        if (transactionId.HasValue)
        {
            var transactionExists = await dbContext.Set<WalletTransaction>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x => x.Id == transactionId.Value && x.TenantId == tenantId &&
                    x.WalletId == walletId && !x.IsDeleted, ct);
            if (!transactionExists)
                return Result<Guid>.Failure("Original transaction was not found for this wallet", 400);
        }

        // A withdrawal transaction can also reference its hold; only settled balance postings qualify.
        var entries = dbContext.Set<WalletLedgerEntry>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.WalletId == walletId && !x.IsDeleted &&
                x.BalanceBucket == WalletBalanceBucket.Available && x.Amount > 0 &&
                x.Operation.TenantId == tenantId && !x.Operation.IsDeleted &&
                x.Operation.Status == WalletOperationStatus.Completed);
        if (caseType == WalletCaseType.Refund)
            entries = entries.Where(x => x.Direction == WalletLedgerDirection.Debit);
        if (transactionId.HasValue)
            entries = entries.Where(x => x.WalletTransactionId == transactionId.Value);
        if (operationId.HasValue)
            entries = entries.Where(x => x.OperationId == operationId.Value);

        var operationIds = await entries.Select(x => x.OperationId).Distinct().Take(2).ToListAsync(ct);
        return operationIds.Count switch
        {
            1 => Result<Guid>.Success(operationIds[0]),
            0 => Result<Guid>.Failure("No eligible completed original operation was found for this wallet and transaction", 400),
            _ => Result<Guid>.Failure("Original transaction links to multiple eligible operations; select the original operation explicitly", 400)
        };
    }
}
