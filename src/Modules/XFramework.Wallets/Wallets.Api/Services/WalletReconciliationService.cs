using System.Text.Json;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public sealed class WalletReconciliationService(
    DbContext dbContext,
    IWalletRequestContextResolver contextResolver) : IWalletReconciliationService
{
    public async Task<Result<WalletReconciliationRunResponse>> RunAsync(
        RunWalletReconciliationRequest request,
        CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
        {
            return Result<WalletReconciliationRunResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        }

        var run = new WalletReconciliationRun
        {
            Id = Guid.NewGuid(),
            TenantId = contextResult.Data!.TenantId,
            Status = WalletReconciliationStatus.Pending,
            StartedAt = DateTime.UtcNow
        };
        dbContext.Set<WalletReconciliationRun>().Add(run);

        var wallets = await dbContext.Set<Wallet>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == contextResult.Data.TenantId &&
                !x.IsDeleted &&
                (!request.WalletId.HasValue || x.Id == request.WalletId.Value))
            .ToListAsync(ct);

        foreach (var wallet in wallets)
        {
            var snapshot = await dbContext.Set<WalletBalanceSnapshot>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == wallet.TenantId && x.WalletId == wallet.Id && !x.IsDeleted, ct);
            AddItemIfDrifted(
                run,
                wallet.Id,
                "wallet_vs_snapshot",
                snapshot?.Balance ?? 0,
                wallet.Balance,
                new { snapshotId = snapshot?.Id });

            var lastEntry = await dbContext.Set<WalletLedgerEntry>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.TenantId == wallet.TenantId && x.WalletId == wallet.Id && !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Sequence)
                .FirstOrDefaultAsync(ct);
            AddItemIfDrifted(
                run,
                wallet.Id,
                "wallet_vs_ledger_running_balance",
                lastEntry?.RunningBalance ?? 0,
                wallet.Balance,
                new { ledgerEntryId = lastEntry?.Id });
        }

        var linkedTransactionIds = await dbContext.Set<WalletLedgerEntry>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == contextResult.Data.TenantId &&
                !x.IsDeleted &&
                x.WalletTransactionId != null)
            .Select(x => x.WalletTransactionId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var transactionDrifts = await dbContext.Set<WalletTransaction>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == contextResult.Data.TenantId &&
                !x.IsDeleted &&
                !linkedTransactionIds.Contains(x.Id))
            .Take(100)
            .ToListAsync(ct);
        foreach (var drift in transactionDrifts)
        {
            run.Items.Add(new WalletReconciliationItem
            {
                Id = Guid.NewGuid(),
                TenantId = contextResult.Data.TenantId,
                RunId = run.Id,
                WalletId = drift.WalletId,
                CheckType = "transaction_without_ledger_entry",
                Status = WalletReconciliationStatus.Drifted,
                ExpectedAmount = drift.Amount,
                ActualAmount = 0,
                DriftAmount = drift.Amount,
                ReferenceNumber = drift.ReferenceNumber,
                DetailsJson = JsonSerializer.Serialize(new { transactionId = drift.Id }),
                RepairSuggestion = "Review historical transaction and create a manual reconciliation note or backfill ledger postings."
            });
        }

        var webhookDrifts = await dbContext.Set<WalletPaymentWebhookEvent>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.DepositRequest)
            .Include(x => x.WithdrawalRequest)
            .Where(x =>
                x.TenantId == contextResult.Data.TenantId &&
                !x.IsDeleted &&
                x.MappedWorkflowStatus != null &&
                ((x.DepositRequest != null && x.DepositRequest.WorkflowStatus != x.MappedWorkflowStatus) ||
                 (x.WithdrawalRequest != null && x.WithdrawalRequest.WorkflowStatus != x.MappedWorkflowStatus)))
            .Take(100)
            .ToListAsync(ct);
        foreach (var drift in webhookDrifts)
        {
            var internalStatus = drift.DepositRequest?.WorkflowStatus ?? drift.WithdrawalRequest?.WorkflowStatus;
            var walletId = drift.DepositRequest?.WalletId ?? drift.WithdrawalRequest?.WalletId ?? Guid.Empty;
            run.Items.Add(new WalletReconciliationItem
            {
                Id = Guid.NewGuid(),
                TenantId = contextResult.Data.TenantId,
                RunId = run.Id,
                WalletId = walletId,
                CheckType = "gateway_status_vs_internal_workflow",
                Status = WalletReconciliationStatus.Drifted,
                ExpectedAmount = drift.MappedWorkflowStatus.HasValue ? (int)drift.MappedWorkflowStatus.Value : 0,
                ActualAmount = internalStatus.HasValue ? (int)internalStatus.Value : 0,
                DriftAmount = drift.MappedWorkflowStatus.HasValue && internalStatus.HasValue
                    ? (int)internalStatus.Value - (int)drift.MappedWorkflowStatus.Value
                    : 0,
                ReferenceNumber = drift.ExternalReference,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    webhookEventId = drift.Id,
                    drift.ProviderKey,
                    drift.ExternalEventId,
                    drift.ProviderStatus,
                    expectedWorkflowStatus = drift.MappedWorkflowStatus?.ToString(),
                    actualWorkflowStatus = internalStatus?.ToString(),
                    drift.DepositRequestId,
                    drift.WithdrawalRequestId
                }),
                RepairSuggestion = "Review provider event and retry webhook settlement or manually mark the workflow reconciled."
            });
        }

        run.CheckedCount = wallets.Count + transactionDrifts.Count + webhookDrifts.Count;
        run.DriftCount = run.Items.Count(x => x.Status == WalletReconciliationStatus.Drifted);
        run.Status = run.DriftCount == 0 ? WalletReconciliationStatus.Matched : WalletReconciliationStatus.Drifted;
        run.CompletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return Result<WalletReconciliationRunResponse>.Success(new WalletReconciliationRunResponse
        {
            RunId = run.Id,
            Status = run.Status,
            CheckedCount = run.CheckedCount,
            DriftCount = run.DriftCount,
            Message = run.DriftCount == 0 ? "Reconciliation matched" : "Reconciliation found drift"
        });
    }

    public async Task<Result<WalletReconciliationItemResponse>> MarkReconciledAsync(
        MarkWalletReconciliationItemRequest request,
        CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
        {
            return Result<WalletReconciliationItemResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        }

        var item = await dbContext.Set<WalletReconciliationItem>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ItemId && x.TenantId == contextResult.Data!.TenantId && !x.IsDeleted, ct);
        if (item is null)
        {
            return Result<WalletReconciliationItemResponse>.NotFound("Reconciliation item not found");
        }

        item.Status = WalletReconciliationStatus.MarkedReconciled;
        item.MarkedReconciledByCredentialId = contextResult.Data!.ActorCredentialId;
        item.MarkedReconciledAt = DateTime.UtcNow;
        item.RepairSuggestion = request.Reason ?? item.RepairSuggestion;
        await dbContext.SaveChangesAsync(ct);

        return Result<WalletReconciliationItemResponse>.Success(ToResponse(item));
    }

    private static void AddItemIfDrifted(
        WalletReconciliationRun run,
        Guid walletId,
        string checkType,
        decimal expected,
        decimal actual,
        object details)
    {
        var drift = actual - expected;
        if (drift == 0)
        {
            return;
        }

        run.Items.Add(new WalletReconciliationItem
        {
            Id = Guid.NewGuid(),
            TenantId = run.TenantId,
            RunId = run.Id,
            WalletId = walletId,
            CheckType = checkType,
            Status = WalletReconciliationStatus.Drifted,
            ExpectedAmount = expected,
            ActualAmount = actual,
            DriftAmount = drift,
            DetailsJson = JsonSerializer.Serialize(details),
            RepairSuggestion = "Review drift details and repair by ledger backfill or manual reconciliation note."
        });
    }

    private static WalletReconciliationItemResponse ToResponse(WalletReconciliationItem item) =>
        new()
        {
            Id = item.Id,
            WalletId = item.WalletId,
            CheckType = item.CheckType,
            Status = item.Status,
            ExpectedAmount = item.ExpectedAmount,
            ActualAmount = item.ActualAmount,
            DriftAmount = item.DriftAmount,
            RepairSuggestion = item.RepairSuggestion
        };
}
