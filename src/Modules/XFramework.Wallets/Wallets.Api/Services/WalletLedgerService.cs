using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public sealed class WalletLedgerService(
    DbContext dbContext,
    IWalletPolicyEvaluator policyEvaluator,
    ILogger<WalletLedgerService> logger) : IWalletLedgerService
{
    private static readonly JsonSerializerOptions HashJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> HasProcessedAsync(
        Guid tenantId,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }

        return await dbContext.Set<WalletOperation>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                !x.IsDeleted &&
                x.IdempotencyKey == idempotencyKey &&
                x.Status == WalletOperationStatus.Completed,
                ct);
    }

    public async Task<Result<WalletLedgerExecutionResult>> ExecuteAsync(
        WalletLedgerExecutionRequest request,
        CancellationToken ct = default)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return Result<WalletLedgerExecutionResult>.Failure(validation, 400);
        }

        var requestHash = string.IsNullOrWhiteSpace(request.RequestHash)
            ? ComputeHash(request)
            : request.RequestHash;

        var replay = await FindCompletedReplayAsync(request.TenantId, request.IdempotencyKey, requestHash, ct);
        if (replay is not null)
        {
            return replay;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct);

        try
        {
            replay = await FindCompletedReplayAsync(request.TenantId, request.IdempotencyKey, requestHash, ct);
            if (replay is not null)
            {
                await transaction.RollbackAsync(ct);
                return replay;
            }

            var walletIds = request.Postings
                .Where(static p => p.WalletId.HasValue)
                .Select(static p => p.WalletId!.Value)
                .Distinct()
                .OrderBy(static id => id)
                .ToList();

            var newWallets = request.NewWallets
                .DistinctBy(static w => w.Id)
                .ToDictionary(static w => w.Id);
            var newWalletIds = newWallets.Keys.ToList();

            if (newWallets.Keys.Any(static id => id == Guid.Empty))
            {
                await transaction.RollbackAsync(ct);
                return Result<WalletLedgerExecutionResult>.Failure("New wallets must have an ID before ledger execution", 400);
            }

            if (newWallets.Values.Any(w => w.TenantId != request.TenantId))
            {
                await transaction.RollbackAsync(ct);
                return Result<WalletLedgerExecutionResult>.Failure("New wallets must belong to the operation tenant", 400);
            }

            await LockWalletRowsAsync(request.TenantId, walletIds, newWalletIds, ct);

            var existingWallets = await dbContext.Set<Wallet>()
                .IgnoreQueryFilters()
                .AsTracking()
                .Where(w =>
                    w.TenantId == request.TenantId &&
                    !w.IsDeleted &&
                    walletIds.Contains(w.Id) &&
                    !newWalletIds.Contains(w.Id))
                .OrderBy(w => w.Id)
                .ToDictionaryAsync(w => w.Id, ct);

            if (newWallets.Keys.Any(existingWallets.ContainsKey))
            {
                await transaction.RollbackAsync(ct);
                return Result<WalletLedgerExecutionResult>.Conflict("One or more new wallets already exist");
            }

            var wallets = existingWallets
                .Concat(newWallets)
                .ToDictionary(static pair => pair.Key, static pair => pair.Value);

            if (walletIds.Any(id => !wallets.ContainsKey(id)))
            {
                await transaction.RollbackAsync(ct);
                return Result<WalletLedgerExecutionResult>.NotFound("One or more wallets were not found");
            }

            var policyResult = await policyEvaluator.EvaluateAsync(
                new WalletPolicyEvaluationContext(request, wallets),
                ct);

            if (!policyResult.IsSuccess)
            {
                return await RejectAndCommitAsync(
                    transaction,
                    request,
                    requestHash,
                    policyResult.Message ?? "Wallet policy rejected the operation",
                    policyResult.StatusCode,
                    policyResult.Data,
                    ct);
            }

            if (policyResult.Data?.IsApproved == false)
            {
                return await RejectAndCommitAsync(
                    transaction,
                    request,
                    requestHash,
                    policyResult.Data.Message ?? "Wallet policy rejected the operation",
                    403,
                    policyResult.Data,
                    ct);
            }

            if (policyResult.Data?.RequiresApproval == true && !request.ApprovalId.HasValue)
            {
                return await RejectAndCommitAsync(
                    transaction,
                    request,
                    requestHash,
                    policyResult.Data.Message ?? "Wallet policy requires maker-checker approval before settlement",
                    409,
                    policyResult.Data,
                    ct);
            }

            if (request.ApprovalId.HasValue)
            {
                var approvalValidation = await ValidateApprovalAsync(request, walletIds, ct);
                if (!approvalValidation.IsSuccess)
                {
                    return await RejectAndCommitAsync(
                        transaction,
                        request,
                        requestHash,
                        approvalValidation.Message ?? "Wallet approval is invalid",
                        approvalValidation.StatusCode,
                        policyResult.Data,
                        ct);
                }
            }

            var operation = new WalletOperation
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                OperationType = request.OperationType,
                Status = WalletOperationStatus.Completed,
                IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey,
                RequestHash = requestHash,
                ReferenceNumber = request.ReferenceNumber,
                CorrelationId = request.CorrelationId,
                ActorCredentialId = request.ActorCredentialId,
                ExternalReference = request.ExternalReference,
                Reason = request.Reason,
                RiskDecision = policyResult.Data?.Decision,
                PolicyDecision = policyResult.Data?.Decision,
                PolicyDecisionJson = policyResult.Data?.DecisionJson,
                RequiresApproval = policyResult.Data?.RequiresApproval == true,
                RiskTier = policyResult.Data?.RiskTier,
                RiskScore = policyResult.Data?.RiskScore,
                RequestedFee = request.RequestedFee,
                CalculatedFee = request.CalculatedFee,
                ApprovalId = request.ApprovalId,
                OriginalOperationId = request.OriginalOperationId,
                CompletedAt = DateTime.UtcNow
            };

            dbContext.Set<WalletOperation>().Add(operation);
            dbContext.Set<Wallet>().AddRange(newWallets.Values);

            var snapshots = await dbContext.Set<WalletBalanceSnapshot>()
                .IgnoreQueryFilters()
                .AsTracking()
                .Where(s => s.TenantId == request.TenantId && !s.IsDeleted && walletIds.Contains(s.WalletId))
                .ToDictionaryAsync(s => s.WalletId, ct);

            var sequence = 0;
            var touchedWallets = new Dictionary<Guid, Wallet>();

            foreach (var posting in request.Postings)
            {
                sequence++;

                var entry = CreateLedgerEntry(operation, posting, request, sequence);
                Wallet? wallet = null;

                if (posting.WalletId.HasValue)
                {
                    wallet = wallets[posting.WalletId.Value];
                    CapturePreviousBalances(entry, wallet);
                    ApplyWalletPosting(wallet, posting);
                    ApplyRunningBalances(posting.WalletTransaction, wallet, entry);
                    var snapshot = UpdateSnapshot(request.TenantId, operation.Id, entry, wallet, snapshots);
                    if (dbContext.Entry(snapshot).State == EntityState.Detached)
                    {
                        dbContext.Set<WalletBalanceSnapshot>().Add(snapshot);
                    }
                    touchedWallets[wallet.Id] = wallet;
                }

                dbContext.Set<WalletLedgerEntry>().Add(entry);
            }

            ApplyTransactionUpdates(request.TransactionUpdates, touchedWallets);
            AddCompatibilityReadModels(request);
            dbContext.Set<WalletOutboxMessage>().Add(CreateOutboxMessage(operation, request));

            if (request.BeforeCommitAsync is not null)
            {
                await request.BeforeCommitAsync(operation, ct);
            }

            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var balances = touchedWallets.ToDictionary(
                static pair => pair.Key,
                static pair => ToBalanceResult(pair.Value));

            return Result<WalletLedgerExecutionResult>.Success(
                new WalletLedgerExecutionResult(operation.Id, false, balances));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Wallet ledger operation failed");
            return Result<WalletLedgerExecutionResult>.Failure("Wallet ledger operation failed", 409);
        }
    }

    private void ApplyTransactionUpdates(
        IReadOnlyList<WalletTransactionStateUpdateRequest> updates,
        IReadOnlyDictionary<Guid, Wallet> touchedWallets)
    {
        foreach (var update in updates)
        {
            if (update.Held.HasValue)
            {
                update.Transaction.Held = update.Held.Value;
            }

            if (update.Released.HasValue)
            {
                update.Transaction.Released = update.Released.Value;
            }

            if (update.UpdateRunningBalances &&
                touchedWallets.TryGetValue(update.WalletId, out var wallet))
            {
                update.Transaction.RunningBalance = wallet.Balance;
                update.Transaction.RunningTotalBalance = wallet.TotalBalance;
                update.Transaction.RunningAvailableBalance = wallet.AvailableBalance;
                update.Transaction.RunningDebitOnHoldBalance = wallet.DebitOnHoldBalance;
                update.Transaction.RunningCreditOnHoldBalance = wallet.CreditOnHoldBalance;
            }

            dbContext.Update(update.Transaction);
        }
    }

    private async Task LockWalletRowsAsync(
        Guid tenantId,
        IReadOnlyList<Guid> walletIds,
        IReadOnlyCollection<Guid> newWalletIds,
        CancellationToken ct)
    {
        var existingIds = walletIds
            .Except(newWalletIds)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();

        if (existingIds.Length == 0)
        {
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT "ID"
            FROM "Wallet"."Wallet"
            WHERE "TenantId" = {tenantId}
              AND "IsDeleted" = false
              AND "ID" = ANY({existingIds})
            ORDER BY "ID"
            FOR UPDATE
            """, ct);
    }

    private async Task<Result<WalletLedgerExecutionResult>?> FindCompletedReplayAsync(
        Guid tenantId,
        string? idempotencyKey,
        string requestHash,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var existing = await dbContext.Set<WalletOperation>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IdempotencyKey == idempotencyKey)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Result<WalletLedgerExecutionResult>.Conflict(
                "Idempotency key was already used with a different request");
        }

        if (existing.Status is WalletOperationStatus.Rejected or WalletOperationStatus.Failed)
        {
            return Result<WalletLedgerExecutionResult>.Failure(
                existing.FailureMessage ?? "Wallet operation was rejected",
                existing.Status == WalletOperationStatus.Rejected ? 403 : 409);
        }

        return Result<WalletLedgerExecutionResult>.Success(
            new WalletLedgerExecutionResult(existing.Id, true, new Dictionary<Guid, WalletBalanceExecutionResult>()),
            "Transaction already processed");
    }

    private async Task<Result<WalletLedgerExecutionResult>> RejectAndCommitAsync(
        IDbContextTransaction transaction,
        WalletLedgerExecutionRequest request,
        string requestHash,
        string message,
        int statusCode,
        WalletPolicyEvaluationResult? policyDecision,
        CancellationToken ct)
    {
        dbContext.Set<WalletOperation>().Add(new WalletOperation
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            OperationType = request.OperationType,
            Status = WalletOperationStatus.Rejected,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey,
            RequestHash = requestHash,
            ReferenceNumber = request.ReferenceNumber,
            CorrelationId = request.CorrelationId,
            ActorCredentialId = request.ActorCredentialId,
            ExternalReference = request.ExternalReference,
            Reason = request.Reason,
            FailureMessage = message,
            RiskDecision = policyDecision?.Decision,
            PolicyDecision = policyDecision?.Decision,
            PolicyDecisionJson = policyDecision?.DecisionJson,
            RequiresApproval = policyDecision?.RequiresApproval == true,
            RiskTier = policyDecision?.RiskTier,
            RiskScore = policyDecision?.RiskScore,
            RequestedFee = request.RequestedFee,
            CalculatedFee = request.CalculatedFee,
            ApprovalId = request.ApprovalId,
            OriginalOperationId = request.OriginalOperationId
        });

        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<WalletLedgerExecutionResult>.Failure(message, statusCode);
    }

    private static string? ValidateRequest(WalletLedgerExecutionRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            return "TenantId is required";
        }

        if (request.Postings.Count == 0)
        {
            return "At least one ledger posting is required";
        }

        if (request.Postings.Any(static p => p.Amount <= 0))
        {
            return "Ledger posting amounts must be greater than zero";
        }

        var debitTotal = request.Postings
            .Where(static p => p.Direction == WalletLedgerDirection.Debit)
            .Sum(static p => p.Amount);
        var creditTotal = request.Postings
            .Where(static p => p.Direction == WalletLedgerDirection.Credit)
            .Sum(static p => p.Amount);

        return debitTotal == creditTotal
            ? null
            : "Ledger postings must balance";
    }

    private static WalletLedgerEntry CreateLedgerEntry(
        WalletOperation operation,
        WalletLedgerPostingRequest posting,
        WalletLedgerExecutionRequest request,
        int sequence)
    {
        if (posting.WalletTransaction is { } transaction && transaction.Id == Guid.Empty)
        {
            transaction.Id = Guid.NewGuid();
        }

        return new WalletLedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            OperationId = operation.Id,
            WalletId = posting.WalletId,
            WalletTransactionId = posting.WalletTransactionId ?? posting.WalletTransaction?.Id,
            CurrencyId = posting.CurrencyId,
            WalletTypeId = posting.WalletTypeId,
            Direction = posting.Direction,
            BalanceBucket = posting.BalanceBucket,
            EntryKind = posting.EntryKind,
            Amount = posting.Amount,
            Sequence = sequence,
            Description = posting.Description,
            ReferenceNumber = posting.ReferenceNumber ?? request.ReferenceNumber,
            CounterpartyType = posting.CounterpartyType,
            CounterpartyReference = posting.CounterpartyReference
        };
    }

    private void AddCompatibilityReadModels(WalletLedgerExecutionRequest request)
    {
        var added = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var posting in request.Postings)
        {
            if (posting.WalletTransaction is not null)
            {
                AddReadModel(posting.WalletTransaction, added);
            }
        }

        foreach (var readModel in request.ReadModels)
        {
            AddReadModel(readModel, added);
        }
    }

    private void AddReadModel(object readModel, HashSet<object> added)
    {
        if (!added.Add(readModel))
        {
            return;
        }

        if (dbContext.Entry(readModel).State == EntityState.Detached)
        {
            dbContext.Add(readModel);
        }
    }

    private static void ApplyWalletPosting(Wallet wallet, WalletLedgerPostingRequest posting)
    {
        switch (posting.BalanceBucket)
        {
            case WalletBalanceBucket.Available:
                ApplyAvailablePosting(wallet, posting);
                break;
            case WalletBalanceBucket.DebitHold:
                ApplyDebitHoldPosting(wallet, posting);
                break;
            case WalletBalanceBucket.CreditHold:
                ApplyCreditHoldPosting(wallet, posting);
                break;
            case WalletBalanceBucket.External:
            case WalletBalanceBucket.Fee:
                break;
            default:
                throw new InvalidOperationException("Unsupported wallet balance bucket");
        }

        if (wallet.MaintainingBalanceRule.HasValue &&
            wallet.Balance < wallet.MaintainingBalanceRule.Value)
        {
            throw new InvalidOperationException(
                $"Balance after operation must not drop below {wallet.MaintainingBalanceRule.Value}");
        }

        if (wallet.TransferableBalance < 0)
        {
            throw new InvalidOperationException("Transferable balance cannot be negative");
        }
    }

    private static void ApplyAvailablePosting(Wallet wallet, WalletLedgerPostingRequest posting)
    {
        if (posting.Direction == WalletLedgerDirection.Credit)
        {
            wallet.Balance += posting.Amount;
            wallet.TransferableBalance += posting.Amount;
            return;
        }

        if (wallet.AvailableBalance < posting.Amount)
        {
            throw new InvalidOperationException("Insufficient funds");
        }

        wallet.Balance -= posting.Amount;
        wallet.TransferableBalance -= posting.Amount;
    }

    private static void ApplyDebitHoldPosting(Wallet wallet, WalletLedgerPostingRequest posting)
    {
        if (posting.Direction == WalletLedgerDirection.Debit)
        {
            if (wallet.AvailableBalance < posting.Amount)
            {
                throw new InvalidOperationException("Insufficient funds for hold");
            }

            wallet.DebitOnHoldBalance += posting.Amount;
            wallet.TransferableBalance -= posting.Amount;
            return;
        }

        if (wallet.DebitOnHoldBalance < posting.Amount)
        {
            throw new InvalidOperationException("Debit hold balance is insufficient");
        }

        wallet.DebitOnHoldBalance -= posting.Amount;
        wallet.TransferableBalance += posting.Amount;
    }

    private static void ApplyCreditHoldPosting(Wallet wallet, WalletLedgerPostingRequest posting)
    {
        if (posting.Direction == WalletLedgerDirection.Credit)
        {
            wallet.CreditOnHoldBalance += posting.Amount;
            return;
        }

        if (wallet.CreditOnHoldBalance < posting.Amount)
        {
            throw new InvalidOperationException("Credit hold balance is insufficient");
        }

        wallet.CreditOnHoldBalance -= posting.Amount;
    }

    private static void CapturePreviousBalances(WalletLedgerEntry entry, Wallet wallet)
    {
        entry.PreviousBalance = wallet.Balance;
        entry.PreviousAvailableBalance = wallet.AvailableBalance;
        entry.PreviousDebitOnHoldBalance = wallet.DebitOnHoldBalance;
        entry.PreviousCreditOnHoldBalance = wallet.CreditOnHoldBalance;
    }

    private static void ApplyRunningBalances(
        WalletTransaction? transaction,
        Wallet wallet,
        WalletLedgerEntry entry)
    {
        entry.RunningBalance = wallet.Balance;
        entry.RunningAvailableBalance = wallet.AvailableBalance;
        entry.RunningDebitOnHoldBalance = wallet.DebitOnHoldBalance;
        entry.RunningCreditOnHoldBalance = wallet.CreditOnHoldBalance;

        if (transaction is null)
        {
            return;
        }

        transaction.PreviousBalance = entry.PreviousBalance ?? transaction.PreviousBalance;
        transaction.PreviousTotalBalance = entry.PreviousBalance ?? transaction.PreviousTotalBalance;
        transaction.PreviousDebitOnHoldBalance = entry.PreviousDebitOnHoldBalance ?? transaction.PreviousDebitOnHoldBalance;
        transaction.PreviousCreditOnHoldBalance = entry.PreviousCreditOnHoldBalance ?? transaction.PreviousCreditOnHoldBalance;
        transaction.RunningBalance = wallet.Balance;
        transaction.RunningTotalBalance = wallet.TotalBalance;
        transaction.RunningAvailableBalance = wallet.AvailableBalance;
        transaction.RunningDebitOnHoldBalance = wallet.DebitOnHoldBalance;
        transaction.RunningCreditOnHoldBalance = wallet.CreditOnHoldBalance;
    }

    private static WalletBalanceSnapshot UpdateSnapshot(
        Guid tenantId,
        Guid operationId,
        WalletLedgerEntry entry,
        Wallet wallet,
        Dictionary<Guid, WalletBalanceSnapshot> snapshots)
    {
        if (!snapshots.TryGetValue(wallet.Id, out var snapshot))
        {
            snapshot = new WalletBalanceSnapshot
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WalletId = wallet.Id,
                WalletTypeId = wallet.WalletTypeId
            };
            snapshots[wallet.Id] = snapshot;
        }

        snapshot.Balance = wallet.Balance;
        snapshot.AvailableBalance = wallet.AvailableBalance;
        snapshot.TransferableBalance = wallet.TransferableBalance;
        snapshot.DebitOnHoldBalance = wallet.DebitOnHoldBalance;
        snapshot.CreditOnHoldBalance = wallet.CreditOnHoldBalance;
        snapshot.TotalBalance = wallet.TotalBalance ?? wallet.Balance;
        snapshot.LastOperationId = operationId;
        snapshot.LastLedgerEntryId = entry.Id;
        snapshot.IsReconciled = true;
        snapshot.DriftAmount = 0;
        snapshot.ReconciledAt = DateTime.UtcNow;

        return snapshot;
    }

    private static WalletOutboxMessage CreateOutboxMessage(
        WalletOperation operation,
        WalletLedgerExecutionRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            operationId = operation.Id,
            tenantId = operation.TenantId,
            operationType = operation.OperationType.ToString(),
            operation.Status,
            operation.ReferenceNumber,
            request.CorrelationId,
            walletId = request.Postings.FirstOrDefault(static p => p.WalletId.HasValue)?.WalletId,
            actorCredentialId = operation.ActorCredentialId,
            postingCount = request.Postings.Count
        }, HashJsonOptions);

        return new WalletOutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = operation.TenantId,
            OperationId = operation.Id,
            EventType = "WalletOperationCompleted",
            AggregateType = nameof(WalletOperation),
            AggregateId = operation.Id,
            PayloadJson = payload,
            Status = WalletOutboxStatus.Pending,
            NextAttemptAt = DateTime.UtcNow
        };
    }

    private static WalletBalanceExecutionResult ToBalanceResult(Wallet wallet) =>
        new(
            wallet.Id,
            wallet.Balance,
            wallet.AvailableBalance,
            wallet.TransferableBalance,
            wallet.DebitOnHoldBalance,
            wallet.CreditOnHoldBalance,
            wallet.TotalBalance ?? wallet.Balance);

    private static string ComputeHash(WalletLedgerExecutionRequest request)
    {
        var hashPayload = new
        {
            request.TenantId,
            request.OperationType,
            request.ActorCredentialId,
            request.ReferenceNumber,
            request.ExternalReference,
            request.Reason,
            postings = request.Postings.Select(static p => new
            {
                p.WalletId,
                p.CurrencyId,
                p.WalletTypeId,
                p.Direction,
                p.BalanceBucket,
                p.EntryKind,
                p.Amount,
                p.ReferenceNumber,
                p.CounterpartyType,
                p.CounterpartyReference
            })
        };

        var json = JsonSerializer.Serialize(hashPayload, HashJsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private async Task<Result> ValidateApprovalAsync(
        WalletLedgerExecutionRequest request,
        IReadOnlyCollection<Guid> walletIds,
        CancellationToken ct)
    {
        var approvalSet = dbContext.Set<WalletApprovalRequest>();
        var approval = approvalSet.Local.FirstOrDefault(x =>
                x.Id == request.ApprovalId!.Value &&
                x.TenantId == request.TenantId &&
                !x.IsDeleted)
            ?? await approvalSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == request.ApprovalId!.Value &&
                x.TenantId == request.TenantId &&
                !x.IsDeleted,
                ct);
        if (approval is null)
        {
            return Result.NotFound("Wallet approval was not found");
        }

        if (approval.Status != WalletApprovalStatus.Approved)
        {
            return Result.Failure("Wallet approval must be approved before settlement", 409);
        }

        if (!IsApprovalCompatible(approval.OperationType, request.OperationType))
        {
            return Result.Failure("Wallet approval does not match the operation type", 400);
        }

        if (approval.WalletId.HasValue && !walletIds.Contains(approval.WalletId.Value))
        {
            return Result.Failure("Wallet approval does not match the target wallet", 400);
        }

        if (RequiresApproverSeparation(request.OperationType) &&
            approval.ApproverCredentialId.HasValue &&
            approval.ApproverCredentialId == approval.RequesterCredentialId)
        {
            return Result.Failure("Wallet approval requires a different approver", 409);
        }

        return Result.Success();
    }

    private static bool IsApprovalCompatible(
        WalletOperationType approvalOperationType,
        WalletOperationType operationType) =>
        approvalOperationType == operationType ||
        (approvalOperationType == WalletOperationType.WithdrawalApproval &&
         operationType is
             WalletOperationType.Hold or
             WalletOperationType.Release or
             WalletOperationType.WithdrawalApproval);

    private static bool RequiresApproverSeparation(WalletOperationType operationType) =>
        operationType is
            WalletOperationType.Transfer or
            WalletOperationType.Reversal or
            WalletOperationType.ManualAdjustment or
            WalletOperationType.Freeze or
            WalletOperationType.Unfreeze or
            WalletOperationType.Close;
}
