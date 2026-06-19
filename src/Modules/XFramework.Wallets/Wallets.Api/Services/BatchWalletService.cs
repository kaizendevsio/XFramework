using System.Diagnostics;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Core.Loggers;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Api.Services;

/// <summary>
/// Batch wallet operations backed by the append-only wallet ledger.
/// </summary>
public sealed class BatchWalletService(
    DbContext dbContext,
    IWalletLedgerService ledgerService,
    IWalletFeeCalculator feeCalculator,
    IWalletFeatureGateService featureGateService,
    ILogger<BatchWalletService> logger) : IBatchWalletService
{
    private const int MaxBatchSize = 1000;

    public async Task<Result<BatchOperationResult>> BatchIncrementAsync(
        List<BatchIncrementRequest> requests,
        WalletRequestContext context,
        bool allowPartialSuccess = false,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateBatch(requests);
        if (validation is not null)
        {
            return validation;
        }

        var feature = await featureGateService.EnsureEnabledAsync(context.TenantId, TenantModuleFeatureKeys.WalletsBatch, cancellationToken);
        if (!feature.IsSuccess)
        {
            return Result<BatchOperationResult>.Failure(feature.Message!, feature.StatusCode);
        }

        logger.BatchWalletOperationStarted("Increment", requests.Count);
        var stopwatch = Stopwatch.StartNew();
        var result = allowPartialSuccess
            ? await ExecutePartialAsync(requests, context, ExecuteIncrementLedgerAsync, cancellationToken)
            : await ExecuteIncrementLedgerAsync(requests, context, cancellationToken);

        return Complete("BatchIncrement", "Increment", result, stopwatch);
    }

    public async Task<Result<BatchOperationResult>> BatchDecrementAsync(
        List<BatchDecrementRequest> requests,
        WalletRequestContext context,
        bool allowPartialSuccess = false,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateBatch(requests);
        if (validation is not null)
        {
            return validation;
        }

        var feature = await featureGateService.EnsureEnabledAsync(context.TenantId, TenantModuleFeatureKeys.WalletsBatch, cancellationToken);
        if (!feature.IsSuccess)
        {
            return Result<BatchOperationResult>.Failure(feature.Message!, feature.StatusCode);
        }

        logger.BatchWalletOperationStarted("Decrement", requests.Count);
        var stopwatch = Stopwatch.StartNew();
        var result = allowPartialSuccess
            ? await ExecutePartialAsync(requests, context, ExecuteDecrementLedgerAsync, cancellationToken)
            : await ExecuteDecrementLedgerAsync(requests, context, cancellationToken);

        return Complete("BatchDecrement", "Decrement", result, stopwatch);
    }

    public async Task<Result<BatchOperationResult>> BatchTransferAsync(
        List<BatchTransferRequest> requests,
        WalletRequestContext context,
        bool allowPartialSuccess = false,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateBatch(requests);
        if (validation is not null)
        {
            return validation;
        }

        var feature = await featureGateService.EnsureEnabledAsync(context.TenantId, TenantModuleFeatureKeys.WalletsBatch, cancellationToken);
        if (!feature.IsSuccess)
        {
            return Result<BatchOperationResult>.Failure(feature.Message!, feature.StatusCode);
        }

        logger.BatchWalletOperationStarted("Transfer", requests.Count);
        var stopwatch = Stopwatch.StartNew();
        var result = allowPartialSuccess
            ? await ExecutePartialAsync(requests, context, ExecuteTransferLedgerAsync, cancellationToken)
            : await ExecuteTransferLedgerAsync(requests, context, cancellationToken);

        return Complete("BatchTransfer", "Transfer", result, stopwatch);
    }

    public async Task<Result<BatchOperationResult>> ProcessTransactionsAsync(
        List<WalletTransaction> transactions,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return Result<BatchOperationResult>.Failure(
            "Direct WalletTransaction batch writes are disabled. Use ledger-backed batch increment, decrement, or transfer operations.",
            409);
    }

    private async Task<Result<BatchOperationResult>> ExecuteIncrementLedgerAsync(
        List<BatchIncrementRequest> requests,
        WalletRequestContext context,
        CancellationToken ct)
    {
        var tenantId = context.TenantId;
        var walletIds = requests
            .Where(static r => r.WalletId != Guid.Empty)
            .Select(static r => r.WalletId)
            .Distinct()
            .ToList();

        var wallets = await dbContext.Set<Wallet>()
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && walletIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, ct);

        var newWallets = new Dictionary<Guid, Wallet>();
        var drafts = wallets.ToDictionary(static pair => pair.Key, static pair => WalletDraft.From(pair.Value));
        var postings = new List<WalletLedgerPostingRequest>();
        var readModels = new List<object>();

        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var validation = ValidateIncrement(request);
            if (validation is not null)
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber, validation);
            }

            if (!wallets.TryGetValue(request.WalletId, out var wallet))
            {
                if (request.WalletTypeId == Guid.Empty)
                {
                    return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber,
                        "Wallet not found and WalletTypeId not provided for auto-creation");
                }

                var actorValidation = AuthorizeNewWalletTarget(context, request.CredentialId);
                if (actorValidation is not null)
                {
                    return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber, actorValidation.Message, actorValidation.StatusCode);
                }

                var walletId = request.WalletId == Guid.Empty ? Guid.NewGuid() : request.WalletId;
                wallet = new Wallet
                {
                    Id = walletId,
                    TenantId = tenantId,
                    CredentialId = request.CredentialId,
                    WalletTypeId = request.WalletTypeId,
                    Balance = 0,
                    TransferableBalance = 0,
                    DebitOnHoldBalance = 0,
                    CreditOnHoldBalance = 0,
                    IsEnabled = true
                };
                wallets[wallet.Id] = wallet;
                newWallets[wallet.Id] = wallet;
                drafts[wallet.Id] = WalletDraft.From(wallet);
            }

            var authorization = AuthorizeWalletTarget(context, wallet, request.CredentialId, requireActorOwnership: true);
            if (authorization is not null)
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber, authorization.Message, authorization.StatusCode);
            }

            if (wallet.MinTransferRule.HasValue && request.Amount < wallet.MinTransferRule.Value)
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber,
                    $"Amount {request.Amount} is below minimum transfer rule {wallet.MinTransferRule.Value}");
            }

            if (wallet.MaxTransferRule.HasValue && request.Amount > wallet.MaxTransferRule.Value)
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber,
                    $"Amount {request.Amount} exceeds maximum transfer rule {wallet.MaxTransferRule.Value}");
            }

            var feeResult = await CalculateBatchFeeAsync(
                tenantId,
                WalletOperationType.Credit,
                wallet.WalletTypeId,
                request.Amount,
                request.Fee,
                ct);
            if (!feeResult.IsSuccess)
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber, feeResult.Message!);
            }

            var fee = feeResult.Data;
            if (fee > request.Amount)
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber,
                    $"Calculated increment fee {fee} exceeds amount {request.Amount}");
            }

            var draft = drafts[wallet.Id];
            var netCredit = request.Amount - fee;
            if (request.OnHold)
            {
                draft.CreditOnHoldBalance += netCredit;
            }
            else
            {
                draft.Balance += netCredit;
                draft.TransferableBalance += netCredit;
            }

            var referenceNumber = CreateReferenceNumber(request.ReferenceNumber);
            var walletTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CredentialId = request.CredentialId,
                WalletId = wallet.Id,
                Amount = request.Amount,
                TransactionFee = fee,
                Remarks = request.Remarks,
                TransactionType = TransactionType.Credit,
                Held = request.OnHold,
                Released = !request.OnHold,
                ReferenceNumber = referenceNumber
            };

            postings.Add(new WalletLedgerPostingRequest
            {
                Direction = WalletLedgerDirection.Debit,
                BalanceBucket = WalletBalanceBucket.External,
                EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                Amount = request.Amount,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = referenceNumber,
                CounterpartyType = "batch-increment-source",
                CounterpartyReference = "wallets",
                Description = "Batch increment source"
            });

            if (netCredit > 0)
            {
                postings.Add(new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    WalletTransaction = walletTransaction,
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = request.OnHold ? WalletBalanceBucket.CreditHold : WalletBalanceBucket.Available,
                    EntryKind = request.OnHold ? WalletLedgerEntryKind.Hold : WalletLedgerEntryKind.Principal,
                    Amount = netCredit,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    Description = request.OnHold ? "Batch held credit" : "Batch available credit"
                });
            }
            else
            {
                ApplyDraftBalances(walletTransaction, draft);
            }

            if (fee > 0)
            {
                postings.Add(CreateFeePosting(fee, wallet.WalletTypeId, referenceNumber, "Batch increment fee"));
            }

            readModels.Add(walletTransaction);
        }

        return await ExecuteLedgerAsync(
            tenantId,
            WalletOperationType.Batch,
            postings,
            readModels,
            newWallets.Values.ToList(),
            requests.Count,
            "batch-increment",
            ct);
    }

    private async Task<Result<BatchOperationResult>> ExecuteDecrementLedgerAsync(
        List<BatchDecrementRequest> requests,
        WalletRequestContext context,
        CancellationToken ct)
    {
        var tenantId = context.TenantId;
        var walletIds = requests.Select(static r => r.WalletId).Distinct().ToList();
        var wallets = await dbContext.Set<Wallet>()
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && walletIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, ct);

        var drafts = wallets.ToDictionary(static pair => pair.Key, static pair => WalletDraft.From(pair.Value));
        var postings = new List<WalletLedgerPostingRequest>();
        var readModels = new List<object>();

        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var validation = ValidateDecrement(request);
            if (validation is not null)
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber, validation);
            }

            if (!wallets.TryGetValue(request.WalletId, out var wallet))
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber, "Wallet not found");
            }

            var authorization = AuthorizeWalletTarget(context, wallet, request.CredentialId, requireActorOwnership: true);
            if (authorization is not null)
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber, authorization.Message, authorization.StatusCode);
            }

            var draft = drafts[wallet.Id];
            var feeResult = await CalculateBatchFeeAsync(
                tenantId,
                WalletOperationType.Debit,
                wallet.WalletTypeId,
                request.Amount,
                request.Fee,
                ct);
            if (!feeResult.IsSuccess)
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber, feeResult.Message!);
            }

            var fee = feeResult.Data;
            var totalDebit = request.Amount + fee;
            var debitValidation = ValidateDraftDebit(draft, wallet, totalDebit, request.OnHold, "decrement");
            if (debitValidation is not null)
            {
                return BatchFailure(requests.Count, i, request.WalletId, request.ReferenceNumber, debitValidation);
            }

            if (request.OnHold)
            {
                draft.DebitOnHoldBalance += totalDebit;
                draft.TransferableBalance -= totalDebit;
            }
            else
            {
                draft.Balance -= totalDebit;
                draft.TransferableBalance -= totalDebit;
            }

            var referenceNumber = CreateReferenceNumber(request.ReferenceNumber);
            var walletTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CredentialId = request.CredentialId,
                WalletId = wallet.Id,
                Amount = request.Amount,
                TransactionFee = fee,
                Remarks = request.Remarks,
                TransactionType = TransactionType.Debit,
                Held = request.OnHold,
                Released = !request.OnHold,
                ReferenceNumber = referenceNumber
            };

            postings.Add(new WalletLedgerPostingRequest
            {
                WalletId = wallet.Id,
                WalletTransaction = walletTransaction,
                Direction = WalletLedgerDirection.Debit,
                BalanceBucket = request.OnHold ? WalletBalanceBucket.DebitHold : WalletBalanceBucket.Available,
                EntryKind = request.OnHold ? WalletLedgerEntryKind.Hold : WalletLedgerEntryKind.Principal,
                Amount = totalDebit,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = referenceNumber,
                Description = request.OnHold ? "Batch held debit" : "Batch available debit"
            });

            postings.Add(new WalletLedgerPostingRequest
            {
                Direction = WalletLedgerDirection.Credit,
                BalanceBucket = WalletBalanceBucket.External,
                EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                Amount = request.Amount,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = referenceNumber,
                CounterpartyType = "batch-decrement-destination",
                CounterpartyReference = "wallets",
                Description = "Batch decrement destination"
            });

            if (fee > 0)
            {
                postings.Add(CreateFeePosting(fee, wallet.WalletTypeId, referenceNumber, "Batch decrement fee"));
            }

            readModels.Add(walletTransaction);
        }

        return await ExecuteLedgerAsync(
            tenantId,
            WalletOperationType.Batch,
            postings,
            readModels,
            [],
            requests.Count,
            "batch-decrement",
            ct);
    }

    private async Task<Result<BatchOperationResult>> ExecuteTransferLedgerAsync(
        List<BatchTransferRequest> requests,
        WalletRequestContext context,
        CancellationToken ct)
    {
        var tenantId = context.TenantId;
        var walletIds = requests
            .SelectMany(static r => new[] { r.FromWalletId, r.ToWalletId })
            .Distinct()
            .ToList();

        var wallets = await dbContext.Set<Wallet>()
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && walletIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, ct);

        var drafts = wallets.ToDictionary(static pair => pair.Key, static pair => WalletDraft.From(pair.Value));
        var postings = new List<WalletLedgerPostingRequest>();
        var readModels = new List<object>();

        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var validation = ValidateTransfer(request);
            if (validation is not null)
            {
                return BatchFailure(requests.Count, i, request.FromWalletId, request.ReferenceNumber, validation);
            }

            if (!wallets.TryGetValue(request.FromWalletId, out var fromWallet))
            {
                return BatchFailure(requests.Count, i, request.FromWalletId, request.ReferenceNumber, "Source wallet not found");
            }

            if (!wallets.TryGetValue(request.ToWalletId, out var toWallet))
            {
                return BatchFailure(requests.Count, i, request.ToWalletId, request.ReferenceNumber, "Destination wallet not found");
            }

            var sourceAuthorization = AuthorizeWalletTarget(context, fromWallet, request.FromCredentialId, requireActorOwnership: true);
            if (sourceAuthorization is not null)
            {
                return BatchFailure(requests.Count, i, request.FromWalletId, request.ReferenceNumber, sourceAuthorization.Message, sourceAuthorization.StatusCode);
            }

            var destinationAuthorization = AuthorizeWalletTarget(context, toWallet, request.ToCredentialId, requireActorOwnership: false);
            if (destinationAuthorization is not null)
            {
                return BatchFailure(requests.Count, i, request.ToWalletId, request.ReferenceNumber, destinationAuthorization.Message, destinationAuthorization.StatusCode);
            }

            var fromDraft = drafts[fromWallet.Id];
            var feeResult = await CalculateBatchFeeAsync(
                tenantId,
                WalletOperationType.Transfer,
                fromWallet.WalletTypeId,
                request.Amount,
                request.Fee,
                ct);
            if (!feeResult.IsSuccess)
            {
                return BatchFailure(requests.Count, i, request.FromWalletId, request.ReferenceNumber, feeResult.Message!);
            }

            var fee = feeResult.Data;
            var totalDebit = request.Amount + fee;
            var debitValidation = ValidateDraftDebit(fromDraft, fromWallet, totalDebit, onHold: false, "transfer");
            if (debitValidation is not null)
            {
                return BatchFailure(requests.Count, i, request.FromWalletId, request.ReferenceNumber, debitValidation);
            }

            fromDraft.Balance -= totalDebit;
            fromDraft.TransferableBalance -= totalDebit;

            var toDraft = drafts[toWallet.Id];
            toDraft.Balance += request.Amount;
            toDraft.TransferableBalance += request.Amount;

            var referenceNumber = CreateReferenceNumber(request.ReferenceNumber);
            var debitTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CredentialId = request.FromCredentialId,
                WalletId = fromWallet.Id,
                Amount = request.Amount,
                TransactionFee = fee,
                Remarks = $"Transfer to wallet {toWallet.Id}: {request.Remarks}",
                TransactionType = TransactionType.Debit,
                Held = false,
                Released = true,
                ReferenceNumber = referenceNumber
            };

            var creditTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CredentialId = request.ToCredentialId,
                WalletId = toWallet.Id,
                Amount = request.Amount,
                TransactionFee = 0,
                Remarks = $"Transfer from wallet {fromWallet.Id}: {request.Remarks}",
                TransactionType = TransactionType.Credit,
                Held = false,
                Released = true,
                ReferenceNumber = referenceNumber
            };

            var walletTransfer = new WalletTransfer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SenderTransactionId = debitTransaction.Id,
                RecipientTransactionId = creditTransaction.Id,
                SenderTransaction = debitTransaction,
                RecipientTransaction = creditTransaction,
                TransactionPurpose = TransactionPurpose.Transfer,
                TransactionFee = fee
            };

            postings.Add(new WalletLedgerPostingRequest
            {
                WalletId = fromWallet.Id,
                WalletTransaction = debitTransaction,
                Direction = WalletLedgerDirection.Debit,
                BalanceBucket = WalletBalanceBucket.Available,
                EntryKind = WalletLedgerEntryKind.Principal,
                Amount = totalDebit,
                WalletTypeId = fromWallet.WalletTypeId,
                ReferenceNumber = referenceNumber,
                Description = "Batch transfer debit"
            });

            postings.Add(new WalletLedgerPostingRequest
            {
                WalletId = toWallet.Id,
                WalletTransaction = creditTransaction,
                Direction = WalletLedgerDirection.Credit,
                BalanceBucket = WalletBalanceBucket.Available,
                EntryKind = WalletLedgerEntryKind.Principal,
                Amount = request.Amount,
                WalletTypeId = toWallet.WalletTypeId,
                ReferenceNumber = referenceNumber,
                Description = "Batch transfer credit"
            });

            if (fee > 0)
            {
                postings.Add(CreateFeePosting(fee, fromWallet.WalletTypeId, referenceNumber, "Batch transfer fee"));
            }

            readModels.Add(debitTransaction);
            readModels.Add(creditTransaction);
            readModels.Add(walletTransfer);
        }

        return await ExecuteLedgerAsync(
            tenantId,
            WalletOperationType.Batch,
            postings,
            readModels,
            [],
            requests.Count,
            "batch-transfer",
            ct);
    }

    private async Task<Result<BatchOperationResult>> ExecuteLedgerAsync(
        Guid tenantId,
        WalletOperationType operationType,
        IReadOnlyList<WalletLedgerPostingRequest> postings,
        IReadOnlyList<object> readModels,
        IReadOnlyList<Wallet> newWallets,
        int totalProcessed,
        string referencePrefix,
        CancellationToken ct)
    {
        var ledgerResult = await ledgerService.ExecuteAsync(
            new WalletLedgerExecutionRequest
            {
                TenantId = tenantId,
                OperationType = operationType,
                ReferenceNumber = $"{referencePrefix}:{Guid.NewGuid():N}",
                Postings = postings,
                NewWallets = newWallets,
                ReadModels = readModels
            },
            ct);

        if (!ledgerResult.IsSuccess)
        {
            return Result<BatchOperationResult>.Failure(
                ledgerResult.Message ?? "Batch ledger operation failed",
                ledgerResult.StatusCode);
        }

        return Result<BatchOperationResult>.Success(new BatchOperationResult
        {
            TotalProcessed = totalProcessed,
            SuccessCount = totalProcessed,
            FailureCount = 0
        });
    }

    private async Task<Result<BatchOperationResult>> ExecutePartialAsync<TRequest>(
        List<TRequest> requests,
        WalletRequestContext context,
        Func<List<TRequest>, WalletRequestContext, CancellationToken, Task<Result<BatchOperationResult>>> executeSingle,
        CancellationToken ct)
        where TRequest : class
    {
        var result = new BatchOperationResult { TotalProcessed = requests.Count };

        for (var i = 0; i < requests.Count; i++)
        {
            var singleResult = await executeSingle([requests[i]], context, ct);
            if (singleResult.IsSuccess)
            {
                result.SuccessCount++;
                continue;
            }

            result.FailureCount++;
            var (walletId, referenceNumber) = GetBatchItemIdentity(requests[i]);
            AddError(result, i, walletId, referenceNumber, singleResult.Message ?? "Batch item failed");
        }

        return Result<BatchOperationResult>.Success(result);
    }

    private Result<BatchOperationResult> Complete(
        string operationName,
        string batchOperationName,
        Result<BatchOperationResult> result,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();

        if (!result.IsSuccess)
        {
            logger.OperationFailed(operationName, "Wallet", Guid.Empty, result.Message ?? "Batch operation failed");
            return result;
        }

        result.Data!.Duration = stopwatch.Elapsed;
        logger.BatchWalletOperationCompleted(batchOperationName, result.Data.SuccessCount, result.Data.TotalProcessed);
        logger.OperationCompleted(operationName, stopwatch.ElapsedMilliseconds);
        return result;
    }

    private static BatchAuthorizationFailure? AuthorizeNewWalletTarget(WalletRequestContext context, Guid credentialId)
    {
        if (credentialId == Guid.Empty)
        {
            return new BatchAuthorizationFailure("CredentialId is required for wallet creation", 400);
        }

        if (!context.IsPrivilegedActor && context.ActorCredentialId != credentialId)
        {
            return new BatchAuthorizationFailure("Actor is not authorized for this batch wallet target", 403);
        }

        return null;
    }

    private static BatchAuthorizationFailure? AuthorizeWalletTarget(
        WalletRequestContext context,
        Wallet wallet,
        Guid requestCredentialId,
        bool requireActorOwnership)
    {
        if (requestCredentialId == Guid.Empty)
        {
            return new BatchAuthorizationFailure("CredentialId is required for batch wallet operations", 400);
        }

        if (wallet.CredentialId != requestCredentialId)
        {
            return new BatchAuthorizationFailure("Wallet does not belong to the requested credential", 400);
        }

        if (requireActorOwnership && !context.IsPrivilegedActor && context.ActorCredentialId != wallet.CredentialId)
        {
            return new BatchAuthorizationFailure("Actor is not authorized for this batch wallet target", 403);
        }

        return null;
    }

    private async Task<Result<decimal>> CalculateBatchFeeAsync(
        Guid tenantId,
        WalletOperationType operationType,
        Guid? walletTypeId,
        decimal amount,
        decimal requestedFee,
        CancellationToken ct)
    {
        var feeResult = await feeCalculator.CalculateAsync(
            tenantId,
            operationType,
            walletTypeId,
            currencyId: null,
            amount,
            requestedFee,
            ct);

        return feeResult.IsSuccess
            ? Result<decimal>.Success(feeResult.Data!.AppliedFee)
            : Result<decimal>.Failure(feeResult.Message!, feeResult.StatusCode);
    }

    private static Result<BatchOperationResult>? ValidateBatch<T>(List<T>? requests)
    {
        if (requests is null || requests.Count == 0)
        {
            return Result<BatchOperationResult>.Failure("Batch requests cannot be null or empty", 400);
        }

        return requests.Count > MaxBatchSize
            ? Result<BatchOperationResult>.Failure(
                $"Batch size exceeds maximum allowed ({MaxBatchSize}). Please split into smaller batches.",
                400)
            : null;
    }

    private static string? ValidateIncrement(BatchIncrementRequest request)
    {
        if (request.Amount <= 0)
        {
            return $"Invalid increment amount: {request.Amount}";
        }

        return request.Fee < 0 || request.Fee > request.Amount
            ? $"Invalid increment fee: {request.Fee}"
            : null;
    }

    private static string? ValidateDecrement(BatchDecrementRequest request)
    {
        if (request.Amount <= 0)
        {
            return $"Invalid decrement amount: {request.Amount}";
        }

        return request.Fee < 0
            ? $"Invalid decrement fee: {request.Fee}"
            : null;
    }

    private static string? ValidateTransfer(BatchTransferRequest request)
    {
        if (request.Amount <= 0)
        {
            return $"Invalid transfer amount: {request.Amount}";
        }

        return request.Fee < 0
            ? $"Invalid transfer fee: {request.Fee}"
            : null;
    }

    private static string? ValidateDraftDebit(
        WalletDraft draft,
        Wallet wallet,
        decimal amount,
        bool onHold,
        string operation)
    {
        if (draft.AvailableBalance < amount)
        {
            return $"Insufficient balance. Available: {draft.AvailableBalance}, Required: {amount}";
        }

        if (draft.TransferableBalance < amount)
        {
            return $"Insufficient transferable balance. Available: {draft.TransferableBalance}, Required: {amount}";
        }

        if (!onHold &&
            wallet.MaintainingBalanceRule.HasValue &&
            draft.Balance - amount < wallet.MaintainingBalanceRule.Value)
        {
            return $"Balance after {operation} must not drop below {wallet.MaintainingBalanceRule.Value}";
        }

        return null;
    }

    private static Result<BatchOperationResult> BatchFailure(
        int totalProcessed,
        int index,
        Guid? walletId,
        string? referenceNumber,
        string message,
        int statusCode = 400)
    {
        var result = new BatchOperationResult
        {
            TotalProcessed = totalProcessed,
            SuccessCount = 0,
            FailureCount = 1
        };
        AddError(result, index, walletId, referenceNumber, message);
        return Result<BatchOperationResult>.Failure(message, statusCode);
    }

    private static WalletLedgerPostingRequest CreateFeePosting(
        decimal fee,
        Guid? walletTypeId,
        string referenceNumber,
        string description) =>
        new()
        {
            Direction = WalletLedgerDirection.Credit,
            BalanceBucket = WalletBalanceBucket.Fee,
            EntryKind = WalletLedgerEntryKind.Fee,
            Amount = fee,
            WalletTypeId = walletTypeId,
            ReferenceNumber = referenceNumber,
            CounterpartyType = "platform-fee",
            CounterpartyReference = "wallets",
            Description = description
        };

    private static void ApplyDraftBalances(WalletTransaction transaction, WalletDraft draft)
    {
        transaction.RunningBalance = draft.Balance;
        transaction.RunningTotalBalance = draft.TotalBalance;
        transaction.RunningAvailableBalance = draft.AvailableBalance;
        transaction.RunningDebitOnHoldBalance = draft.DebitOnHoldBalance;
        transaction.RunningCreditOnHoldBalance = draft.CreditOnHoldBalance;
    }

    private static string CreateReferenceNumber(string? referenceNumber) =>
        string.IsNullOrWhiteSpace(referenceNumber)
            ? Guid.NewGuid().ToString()
            : referenceNumber;

    private static (Guid? WalletId, string? ReferenceNumber) GetBatchItemIdentity<TRequest>(TRequest request) =>
        request switch
        {
            BatchIncrementRequest increment => (increment.WalletId, increment.ReferenceNumber),
            BatchDecrementRequest decrement => (decrement.WalletId, decrement.ReferenceNumber),
            BatchTransferRequest transfer => (transfer.FromWalletId, transfer.ReferenceNumber),
            _ => (null, null)
        };

    private static void AddError(
        BatchOperationResult result,
        int index,
        Guid? walletId,
        string? referenceNumber,
        string message)
    {
        result.Errors.Add(new BatchOperationError
        {
            ItemIndex = index,
            WalletId = walletId,
            ReferenceNumber = referenceNumber,
            ErrorMessage = message
        });
    }

    private sealed class WalletDraft
    {
        public decimal Balance { get; set; }
        public decimal TransferableBalance { get; set; }
        public decimal DebitOnHoldBalance { get; set; }
        public decimal CreditOnHoldBalance { get; set; }
        public decimal AvailableBalance => Balance - DebitOnHoldBalance;
        public decimal TotalBalance => Balance + CreditOnHoldBalance - DebitOnHoldBalance;

        public static WalletDraft From(Wallet wallet) =>
            new()
            {
                Balance = wallet.Balance,
                TransferableBalance = wallet.TransferableBalance,
                DebitOnHoldBalance = wallet.DebitOnHoldBalance,
                CreditOnHoldBalance = wallet.CreditOnHoldBalance
            };
    }

    private sealed record BatchAuthorizationFailure(string Message, int StatusCode);
}
