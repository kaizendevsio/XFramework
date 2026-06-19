using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using Payments.Core.Services;
using Payments.Domain.Shared.Contracts;
using Payments.Domain.Shared.Contracts.Requests.Create;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Api.Services;

public sealed class WalletWorkflowService(
    DbContext dbContext,
    IWalletRequestContextResolver contextResolver,
    IWalletFeatureGateService featureGateService,
    IWalletFeeCalculator feeCalculator,
    IWalletLedgerService ledgerService,
    PaymentGatewayService paymentGatewayService,
    IConfiguration configuration)
    : IWalletWorkflowService,
      IWalletApprovalWorkflowService,
      IWalletCaseWorkflowService,
      IWalletReportingService
{
    public async Task<Result<WalletWorkflowResponse>> CreateDepositAsync(
        CreateDepositWorkflowRequest request,
        CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request, request.CredentialId);
        if (!contextResult.IsSuccess) return Result<WalletWorkflowResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsDeposits, ct);
        if (!feature.IsSuccess) return Failure<WalletWorkflowResponse>(feature);

        if (request.Amount <= 0)
        {
            return Result<WalletWorkflowResponse>.Failure("Amount must be greater than zero", 400);
        }

        var requestedWalletTypeId = request.WalletTypeId;
        if (request.WalletId.HasValue)
        {
            var walletResult = await ResolveExistingDepositWalletAsync(
                contextResult.Data!.TenantId,
                request.WalletId.Value,
                request.CredentialId,
                request.WalletTypeId,
                ct);
            if (!walletResult.IsSuccess)
            {
                return Result<WalletWorkflowResponse>.Failure(walletResult.Message!, walletResult.StatusCode);
            }

            requestedWalletTypeId = walletResult.Data!.WalletTypeId ?? request.WalletTypeId;
        }
        else if (!request.WalletTypeId.HasValue)
        {
            return Result<WalletWorkflowResponse>.Failure(
                "Wallet type is required to create a deposit without an existing wallet",
                400);
        }

        var feeResult = await feeCalculator.CalculateAsync(
            contextResult.Data!.TenantId,
            WalletOperationType.DepositApproval,
            requestedWalletTypeId,
            request.CurrencyId,
            request.Amount,
            request.RequestedFee,
            ct);
        if (!feeResult.IsSuccess)
        {
            return Result<WalletWorkflowResponse>.Failure(feeResult.Message!, feeResult.StatusCode);
        }

        var reference = string.IsNullOrWhiteSpace(request.ExternalReference)
            ? $"dep-{Guid.NewGuid():N}"
            : request.ExternalReference;

        var deposit = new DepositRequest
        {
            Id = Guid.NewGuid(),
            TenantId = contextResult.Data.TenantId,
            CredentialId = request.CredentialId,
            WalletId = request.WalletId,
            WalletTypeId = requestedWalletTypeId,
            SourceCurrencyId = request.CurrencyId,
            GatewayId = request.GatewayId,
            Amount = request.Amount,
            RequestedFee = feeResult.Data!.RequestedFee,
            CalculatedFee = feeResult.Data.CalculatedFee,
            ConvenienceFee = feeResult.Data.AppliedFee,
            Address = request.Address,
            Remarks = request.Remarks,
            ReferenceNo = reference,
            ExternalReference = reference,
            ExpiryDate = request.ExpiryDate,
            DepositStatus = (short)DepositStatus.PendingPayment,
            WorkflowStatus = WalletWorkflowStatus.PendingApproval,
            RequestedByCredentialId = contextResult.Data.ActorCredentialId ?? request.CredentialId,
            RawRequestData = JsonSerializer.Serialize(request)
        };

        var approval = CreateApproval(
            deposit.TenantId,
            WalletOperationType.DepositApproval,
            deposit.WalletId,
            deposit.RequestedByCredentialId ?? request.CredentialId,
            request.Amount,
            request.Remarks);
        dbContext.Set<WalletApprovalRequest>().Add(approval);
        deposit.ApprovalId = approval.Id;

        dbContext.Set<DepositRequest>().Add(deposit);
        await dbContext.SaveChangesAsync(ct);

        var providerResult = await TryInitiateDepositProviderAsync(deposit, ct);
        if (!providerResult.IsSuccess)
        {
            return Result<WalletWorkflowResponse>.Failure(providerResult.Message!, providerResult.StatusCode);
        }

        return Result<WalletWorkflowResponse>.Success(ToDepositResponse(deposit, "Deposit request created"));
    }

    public Task<Result<WalletWorkflowResponse>> ValidateDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default) =>
        TransitionDepositAsync(request, [WalletWorkflowStatus.PendingValidation, WalletWorkflowStatus.PendingApproval], WalletWorkflowStatus.PendingApproval, ct);

    public Task<Result<WalletWorkflowResponse>> ApproveDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default) =>
        TransitionDepositAsync(request, [WalletWorkflowStatus.PendingApproval], WalletWorkflowStatus.Approved, ct, approve: true);

    public Task<Result<WalletWorkflowResponse>> RejectDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default) =>
        TransitionDepositAsync(request, [WalletWorkflowStatus.PendingApproval, WalletWorkflowStatus.Approved], WalletWorkflowStatus.Rejected, ct, reject: true);

    public async Task<Result<WalletWorkflowResponse>> SettleDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default)
    {
        var load = await LoadDepositAsync(request, true, ct);
        if (!load.IsSuccess) return Result<WalletWorkflowResponse>.Failure(load.Message!, load.StatusCode);

        var deposit = load.Data!.Entity;
        if (deposit.WorkflowStatus is WalletWorkflowStatus.Completed)
        {
            return Result<WalletWorkflowResponse>.Success(ToDepositResponse(deposit, "Deposit already settled"));
        }

        if (deposit.WorkflowStatus is not (WalletWorkflowStatus.Approved or WalletWorkflowStatus.Settling))
        {
            return Result<WalletWorkflowResponse>.Failure("Deposit must be approved before settlement", 400);
        }

        var wallet = await ResolveDepositWalletAsync(deposit, ct);
        if (!wallet.IsSuccess) return Result<WalletWorkflowResponse>.Failure(wallet.Message!, wallet.StatusCode);

        var amount = deposit.Amount ?? 0;
        var fee = deposit.ConvenienceFee ?? deposit.CalculatedFee ?? deposit.RequestedFee ?? 0;
        if (fee > amount)
        {
            return Result<WalletWorkflowResponse>.Failure("Fee cannot exceed deposit amount", 400);
        }

        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = deposit.TenantId,
            CredentialId = deposit.CredentialId,
            WalletId = wallet.Data!.Id,
            Amount = amount,
            TransactionFee = fee,
            Remarks = deposit.Remarks,
            Description = "Deposit settlement",
            TransactionType = TransactionType.Credit,
            Held = false,
            Released = true,
            ReferenceNumber = deposit.ReferenceNo
        };

        var postings = new List<WalletLedgerPostingRequest>
        {
            new()
            {
                Direction = WalletLedgerDirection.Debit,
                BalanceBucket = WalletBalanceBucket.External,
                EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                Amount = amount,
                CurrencyId = deposit.SourceCurrencyId,
                WalletTypeId = wallet.Data.WalletTypeId,
                ReferenceNumber = deposit.ReferenceNo,
                CounterpartyType = "payment-provider",
                CounterpartyReference = request.ProviderTransactionId ?? request.ExternalReference ?? deposit.ExternalReference,
                Description = "Provider deposit settlement"
            }
        };

        if (amount - fee > 0)
        {
            postings.Add(new WalletLedgerPostingRequest
            {
                WalletId = wallet.Data.Id,
                WalletTransaction = transaction,
                Direction = WalletLedgerDirection.Credit,
                BalanceBucket = WalletBalanceBucket.Available,
                EntryKind = WalletLedgerEntryKind.Principal,
                Amount = amount - fee,
                CurrencyId = deposit.SourceCurrencyId,
                WalletTypeId = wallet.Data.WalletTypeId,
                ReferenceNumber = deposit.ReferenceNo,
                Description = "Deposit credit"
            });
        }

        if (fee > 0)
        {
            postings.Add(new WalletLedgerPostingRequest
            {
                Direction = WalletLedgerDirection.Credit,
                BalanceBucket = WalletBalanceBucket.Fee,
                EntryKind = WalletLedgerEntryKind.Fee,
                Amount = fee,
                CurrencyId = deposit.SourceCurrencyId,
                WalletTypeId = wallet.Data.WalletTypeId,
                ReferenceNumber = deposit.ReferenceNo,
                CounterpartyType = "platform-fee",
                CounterpartyReference = "wallets",
                Description = "Deposit fee"
            });
        }

        var ledgerResult = await ledgerService.ExecuteAsync(new WalletLedgerExecutionRequest
        {
            TenantId = deposit.TenantId,
            OperationType = WalletOperationType.DepositApproval,
            ActorCredentialId = deposit.ApprovedByCredentialId ?? deposit.RequestedByCredentialId ?? deposit.CredentialId,
            IdempotencyKey = request.IdempotencyKey ?? $"deposit:settle:{deposit.Id}:{request.ProviderEventId ?? deposit.ProviderEventId ?? deposit.ReferenceNo}",
            ReferenceNumber = deposit.ReferenceNo,
            ExternalReference = request.ExternalReference ?? deposit.ExternalReference,
            Reason = request.Reason ?? deposit.Remarks,
            RequestedFee = deposit.RequestedFee,
            CalculatedFee = deposit.CalculatedFee,
            ApprovalId = deposit.ApprovalId,
            NewWallets = wallet.Data.CreatedAt == default ? [wallet.Data] : [],
            Postings = postings,
            ReadModels = [transaction],
            BeforeCommitAsync = async (operation, callbackCt) =>
            {
                deposit.WorkflowStatus = WalletWorkflowStatus.Completed;
                deposit.DepositStatus = (short)DepositStatus.Paid;
                deposit.WalletId = wallet.Data.Id;
                deposit.SettlementOperationId = operation.Id;
                deposit.SettlementTransactionId = transaction.Id;
                deposit.ProviderEventId = request.ProviderEventId ?? deposit.ProviderEventId;
                deposit.ProviderTransactionId = request.ProviderTransactionId ?? deposit.ProviderTransactionId;
                deposit.ProviderStatus = request.ProviderStatus ?? deposit.ProviderStatus;
                deposit.RawResponseData = request.RawProviderPayloadJson ?? deposit.RawResponseData;
                deposit.SettledAt = DateTime.UtcNow;

                await LinkWebhookAsync(
                    request.WebhookEventId,
                    deposit.TenantId,
                    depositRequestId: deposit.Id,
                    withdrawalRequestId: null,
                    operation.Id,
                    processingError: null,
                    callbackCt);
            }
        }, ct);

        if (!ledgerResult.IsSuccess)
        {
            return Result<WalletWorkflowResponse>.Failure(ledgerResult.Message!, ledgerResult.StatusCode);
        }

        return Result<WalletWorkflowResponse>.Success(ToDepositResponse(deposit, "Deposit settled"));
    }

    public Task<Result<WalletWorkflowResponse>> FailDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default) =>
        TransitionDepositAsync(request, [WalletWorkflowStatus.PendingApproval, WalletWorkflowStatus.Approved, WalletWorkflowStatus.Settling], WalletWorkflowStatus.Failed, ct, fail: true);

    public Task<Result<WalletWorkflowResponse>> CancelDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default) =>
        TransitionDepositAsync(request, [WalletWorkflowStatus.PendingApproval, WalletWorkflowStatus.Approved], WalletWorkflowStatus.Cancelled, ct, cancel: true);

    public async Task<Result<WalletWorkflowResponse>> CreateWithdrawalAsync(CreateWithdrawalWorkflowRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request, request.CredentialId);
        if (!contextResult.IsSuccess) return Result<WalletWorkflowResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsWithdrawals, ct);
        if (!feature.IsSuccess) return Failure<WalletWorkflowResponse>(feature);

        if (request.Amount <= 0)
        {
            return Result<WalletWorkflowResponse>.Failure("Amount must be greater than zero", 400);
        }

        var wallet = await dbContext.Set<Wallet>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == contextResult.Data!.TenantId &&
                !x.IsDeleted &&
                x.Id == request.WalletId,
                ct);
        if (wallet is null)
        {
            return Result<WalletWorkflowResponse>.NotFound("Wallet not found");
        }

        if (wallet.CredentialId != request.CredentialId)
        {
            return Result<WalletWorkflowResponse>.Forbidden("Credential does not own the wallet");
        }

        var feeResult = await feeCalculator.CalculateAsync(
            contextResult.Data!.TenantId,
            WalletOperationType.WithdrawalApproval,
            wallet.WalletTypeId,
            request.CurrencyId,
            request.Amount,
            request.RequestedFee,
            ct);
        if (!feeResult.IsSuccess)
        {
            return Result<WalletWorkflowResponse>.Failure(feeResult.Message!, feeResult.StatusCode);
        }

        var totalDebit = request.Amount + feeResult.Data!.AppliedFee;
        if (wallet.AvailableBalance < totalDebit)
        {
            return Result<WalletWorkflowResponse>.Failure("Insufficient funds", 400);
        }

        var reference = string.IsNullOrWhiteSpace(request.ExternalReference)
            ? $"wd-{Guid.NewGuid():N}"
            : request.ExternalReference;
        var withdrawal = new WithdrawalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = contextResult.Data.TenantId,
            CredentialId = request.CredentialId,
            WalletId = request.WalletId,
            GatewayId = request.GatewayId,
            Amount = request.Amount,
            Fee = feeResult.Data.AppliedFee,
            RequestedFee = feeResult.Data.RequestedFee,
            CalculatedFee = feeResult.Data.CalculatedFee,
            Address = request.Address,
            Remarks = request.Remarks,
            ReferenceNumber = reference,
            ExternalReference = reference,
            WithdrawalStatus = TransactionStatus.Pending,
            WorkflowStatus = WalletWorkflowStatus.PendingApproval,
            RequestedByCredentialId = contextResult.Data.ActorCredentialId ?? request.CredentialId,
            RawRequestData = JsonSerializer.Serialize(request)
        };

        var approval = CreateApproval(
            withdrawal.TenantId,
            WalletOperationType.WithdrawalApproval,
            withdrawal.WalletId,
            withdrawal.RequestedByCredentialId ?? request.CredentialId,
            totalDebit,
            request.Remarks);
        dbContext.Set<WalletApprovalRequest>().Add(approval);
        withdrawal.ApprovalId = approval.Id;
        dbContext.Set<WithdrawalRequest>().Add(withdrawal);
        await dbContext.SaveChangesAsync(ct);

        return Result<WalletWorkflowResponse>.Success(ToWithdrawalResponse(withdrawal, "Withdrawal request created"));
    }

    public Task<Result<WalletWorkflowResponse>> ValidateWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default) =>
        TransitionWithdrawalAsync(request, [WalletWorkflowStatus.PendingValidation, WalletWorkflowStatus.PendingApproval], WalletWorkflowStatus.PendingApproval, ct);

    public async Task<Result<WalletWorkflowResponse>> ApproveWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default)
    {
        var load = await LoadWithdrawalAsync(request, true, ct);
        if (!load.IsSuccess) return Result<WalletWorkflowResponse>.Failure(load.Message!, load.StatusCode);

        var withdrawal = load.Data!.Entity;
        if (withdrawal.HoldOperationId.HasValue)
        {
            return Result<WalletWorkflowResponse>.Success(ToWithdrawalResponse(withdrawal, "Withdrawal already approved and held"));
        }

        if (withdrawal.WorkflowStatus is not WalletWorkflowStatus.PendingApproval)
        {
            return Result<WalletWorkflowResponse>.Failure(
                $"Cannot move withdrawal from {withdrawal.WorkflowStatus} to {WalletWorkflowStatus.Approved}",
                400);
        }

        var wallet = await dbContext.Set<Wallet>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(x => x.Id == withdrawal.WalletId && x.TenantId == withdrawal.TenantId, ct);

        var amount = (withdrawal.Amount ?? 0) + (withdrawal.Fee ?? withdrawal.CalculatedFee ?? 0);
        var approvalActor = await ResolveApprovalDecisionActorAsync(
            withdrawal.ApprovalId,
            withdrawal.TenantId,
            load.Data.Context,
            withdrawal.RequestedByCredentialId ?? withdrawal.CredentialId,
            ct);
        if (!approvalActor.IsSuccess)
        {
            return Result<WalletWorkflowResponse>.Failure(approvalActor.Message!, approvalActor.StatusCode);
        }

        var actorCredentialId = approvalActor.Data;
        var approval = withdrawal.ApprovalId.HasValue
            ? await dbContext.Set<WalletApprovalRequest>()
                .IgnoreQueryFilters()
                .AsTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == withdrawal.ApprovalId.Value &&
                    x.TenantId == withdrawal.TenantId &&
                    !x.IsDeleted,
                    ct)
            : null;
        ApplyApprovalDecision(approval, WalletApprovalStatus.Approved, actorCredentialId, request.Reason);

        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = withdrawal.TenantId,
            CredentialId = withdrawal.CredentialId,
            WalletId = withdrawal.WalletId,
            Amount = withdrawal.Amount ?? 0,
            TransactionFee = withdrawal.Fee ?? withdrawal.CalculatedFee ?? 0,
            Remarks = withdrawal.Remarks,
            Description = "Withdrawal hold",
            TransactionType = TransactionType.Debit,
            Held = true,
            Released = false,
            ReferenceNumber = withdrawal.ReferenceNumber
        };

        var holdResult = await ledgerService.ExecuteAsync(new WalletLedgerExecutionRequest
        {
            TenantId = withdrawal.TenantId,
            OperationType = WalletOperationType.Hold,
            ActorCredentialId = actorCredentialId,
            IdempotencyKey = $"withdrawal:hold:{withdrawal.Id}",
            ReferenceNumber = withdrawal.ReferenceNumber,
            ExternalReference = withdrawal.ExternalReference,
            Reason = request.Reason ?? withdrawal.Remarks,
            RequestedFee = withdrawal.RequestedFee,
            CalculatedFee = withdrawal.CalculatedFee,
            ApprovalId = withdrawal.ApprovalId,
            Postings =
            [
                new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    WalletTransaction = transaction,
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.DebitHold,
                    EntryKind = WalletLedgerEntryKind.Hold,
                    Amount = amount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = withdrawal.ReferenceNumber,
                    Description = "Withdrawal debit hold"
                },
                new WalletLedgerPostingRequest
                {
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.External,
                    EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                    Amount = amount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = withdrawal.ReferenceNumber,
                    CounterpartyType = "withdrawal-hold",
                    CounterpartyReference = withdrawal.Id.ToString(),
                    Description = "Withdrawal hold counterparty"
                }
            ],
            ReadModels = [transaction],
            BeforeCommitAsync = (operation, _) =>
            {
                withdrawal.WorkflowStatus = WalletWorkflowStatus.Approved;
                withdrawal.ApprovedByCredentialId = actorCredentialId;
                withdrawal.ApprovedAt = DateTime.UtcNow;
                withdrawal.WithdrawalStatus = TransactionStatus.Accepted;
                withdrawal.HoldOperationId = operation.Id;
                withdrawal.SettlementTransactionId = transaction.Id;
                ApplyApprovalDecision(approval, WalletApprovalStatus.Approved, actorCredentialId, request.Reason);
                return Task.CompletedTask;
            }
        }, ct);

        if (!holdResult.IsSuccess)
        {
            return Result<WalletWorkflowResponse>.Failure(holdResult.Message!, holdResult.StatusCode);
        }

        var providerResult = await TryInitiateWithdrawalProviderAsync(withdrawal, request, ct);
        if (providerResult is not null)
        {
            return providerResult;
        }

        return Result<WalletWorkflowResponse>.Success(ToWithdrawalResponse(withdrawal, "Withdrawal approved and held"));
    }

    public Task<Result<WalletWorkflowResponse>> RejectWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default) =>
        CompleteWithdrawalWithoutSettlementAsync(
            request,
            [WalletWorkflowStatus.PendingApproval, WalletWorkflowStatus.Approved],
            WalletWorkflowStatus.Rejected,
            TransactionStatus.Rejected,
            ct);

    public async Task<Result<WalletWorkflowResponse>> SettleWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default)
    {
        var load = await LoadWithdrawalAsync(request, true, ct);
        if (!load.IsSuccess) return Result<WalletWorkflowResponse>.Failure(load.Message!, load.StatusCode);

        var withdrawal = load.Data!.Entity;
        if (withdrawal.WorkflowStatus is WalletWorkflowStatus.Completed)
        {
            return Result<WalletWorkflowResponse>.Success(ToWithdrawalResponse(withdrawal, "Withdrawal already settled"));
        }

        if (withdrawal.WorkflowStatus is not (WalletWorkflowStatus.Approved or WalletWorkflowStatus.Settling))
        {
            return Result<WalletWorkflowResponse>.Failure("Withdrawal must be approved before settlement", 400);
        }

        var wallet = await dbContext.Set<Wallet>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == withdrawal.WalletId && x.TenantId == withdrawal.TenantId && !x.IsDeleted, ct);
        if (wallet is null)
        {
            return Result<WalletWorkflowResponse>.NotFound("Wallet not found");
        }

        var heldTransaction = withdrawal.SettlementTransactionId.HasValue
            ? await dbContext.Set<WalletTransaction>()
                .IgnoreQueryFilters()
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == withdrawal.SettlementTransactionId && x.TenantId == withdrawal.TenantId, ct)
            : null;

        var principalAmount = withdrawal.Amount ?? 0;
        var feeAmount = withdrawal.Fee ?? withdrawal.CalculatedFee ?? 0;
        var totalDebit = principalAmount + feeAmount;
        var postings = new List<WalletLedgerPostingRequest>();
        if (heldTransaction is not null && withdrawal.HoldOperationId.HasValue)
        {
            postings.Add(new WalletLedgerPostingRequest
            {
                WalletId = wallet.Id,
                WalletTransactionId = heldTransaction.Id,
                Direction = WalletLedgerDirection.Credit,
                BalanceBucket = WalletBalanceBucket.DebitHold,
                EntryKind = WalletLedgerEntryKind.Hold,
                Amount = totalDebit,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = withdrawal.ReferenceNumber,
                Description = "Release withdrawal debit hold"
            });
            postings.Add(new WalletLedgerPostingRequest
            {
                WalletId = wallet.Id,
                WalletTransactionId = heldTransaction.Id,
                Direction = WalletLedgerDirection.Debit,
                BalanceBucket = WalletBalanceBucket.Available,
                EntryKind = WalletLedgerEntryKind.Principal,
                Amount = totalDebit,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = withdrawal.ReferenceNumber,
                Description = "Capture withdrawal debit"
            });
            if (feeAmount > 0)
            {
                postings.Add(new WalletLedgerPostingRequest
                {
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.External,
                    EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                    Amount = feeAmount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = withdrawal.ReferenceNumber,
                    CounterpartyType = "payment-provider",
                    CounterpartyReference = request.ProviderTransactionId ?? withdrawal.ExternalReference,
                    Description = "Withdrawal fee provider offset"
                });
            }
        }
        else
        {
            postings.Add(new WalletLedgerPostingRequest
            {
                WalletId = wallet.Id,
                Direction = WalletLedgerDirection.Debit,
                BalanceBucket = WalletBalanceBucket.Available,
                EntryKind = WalletLedgerEntryKind.Principal,
                Amount = totalDebit,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = withdrawal.ReferenceNumber,
                Description = "Withdrawal debit"
            });
            postings.Add(new WalletLedgerPostingRequest
            {
                Direction = WalletLedgerDirection.Credit,
                BalanceBucket = WalletBalanceBucket.External,
                EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                Amount = principalAmount,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = withdrawal.ReferenceNumber,
                CounterpartyType = "payment-provider",
                CounterpartyReference = request.ProviderTransactionId ?? withdrawal.ExternalReference,
                Description = "Provider withdrawal settlement"
            });
        }

        if (feeAmount > 0)
        {
            postings.Add(new WalletLedgerPostingRequest
            {
                Direction = WalletLedgerDirection.Credit,
                BalanceBucket = WalletBalanceBucket.Fee,
                EntryKind = WalletLedgerEntryKind.Fee,
                Amount = feeAmount,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = withdrawal.ReferenceNumber,
                CounterpartyType = "platform-fee",
                CounterpartyReference = "wallets",
                Description = "Withdrawal fee"
            });
        }

        var ledgerResult = await ledgerService.ExecuteAsync(new WalletLedgerExecutionRequest
        {
            TenantId = withdrawal.TenantId,
            OperationType = WalletOperationType.WithdrawalApproval,
            ActorCredentialId = withdrawal.ApprovedByCredentialId ?? withdrawal.RequestedByCredentialId ?? withdrawal.CredentialId,
            IdempotencyKey = request.IdempotencyKey ?? $"withdrawal:settle:{withdrawal.Id}:{request.ProviderEventId ?? withdrawal.ProviderEventId ?? withdrawal.ReferenceNumber}",
            ReferenceNumber = withdrawal.ReferenceNumber,
            ExternalReference = request.ExternalReference ?? withdrawal.ExternalReference,
            Reason = request.Reason ?? withdrawal.Remarks,
            RequestedFee = withdrawal.RequestedFee,
            CalculatedFee = withdrawal.CalculatedFee,
            ApprovalId = withdrawal.ApprovalId,
            Postings = postings,
            TransactionUpdates = heldTransaction is null
                ? []
                :
                [
                    new WalletTransactionStateUpdateRequest
                    {
                        Transaction = heldTransaction,
                        WalletId = wallet.Id,
                        Held = false,
                        Released = true
                    }
                ],
            BeforeCommitAsync = async (operation, callbackCt) =>
            {
                withdrawal.WorkflowStatus = WalletWorkflowStatus.Completed;
                withdrawal.WithdrawalStatus = TransactionStatus.Completed;
                withdrawal.SettlementOperationId = operation.Id;
                withdrawal.ProviderEventId = request.ProviderEventId ?? withdrawal.ProviderEventId;
                withdrawal.ProviderTransactionId = request.ProviderTransactionId ?? withdrawal.ProviderTransactionId;
                withdrawal.ProviderStatus = request.ProviderStatus ?? withdrawal.ProviderStatus;
                withdrawal.RawResponseData = request.RawProviderPayloadJson ?? withdrawal.RawResponseData;
                withdrawal.SettledAt = DateTime.UtcNow;

                await LinkWebhookAsync(
                    request.WebhookEventId,
                    withdrawal.TenantId,
                    depositRequestId: null,
                    withdrawalRequestId: withdrawal.Id,
                    operation.Id,
                    processingError: null,
                    callbackCt);
            }
        }, ct);

        if (!ledgerResult.IsSuccess)
        {
            return Result<WalletWorkflowResponse>.Failure(ledgerResult.Message!, ledgerResult.StatusCode);
        }

        return Result<WalletWorkflowResponse>.Success(ToWithdrawalResponse(withdrawal, "Withdrawal settled"));
    }

    public Task<Result<WalletWorkflowResponse>> FailWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default) =>
        CompleteWithdrawalWithoutSettlementAsync(
            request,
            [WalletWorkflowStatus.PendingApproval, WalletWorkflowStatus.Approved, WalletWorkflowStatus.Settling],
            WalletWorkflowStatus.Failed,
            TransactionStatus.Failed,
            ct);

    public Task<Result<WalletWorkflowResponse>> CancelWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default) =>
        CompleteWithdrawalWithoutSettlementAsync(
            request,
            [WalletWorkflowStatus.PendingApproval, WalletWorkflowStatus.Approved],
            WalletWorkflowStatus.Cancelled,
            TransactionStatus.Cancelled,
            ct);

    private async Task<Result<WalletWorkflowResponse>> CompleteWithdrawalWithoutSettlementAsync(
        WalletWorkflowActionRequest request,
        IReadOnlyCollection<WalletWorkflowStatus> allowedStatuses,
        WalletWorkflowStatus workflowStatus,
        TransactionStatus transactionStatus,
        CancellationToken ct)
    {
        var load = await LoadWithdrawalAsync(request, true, ct);
        if (!load.IsSuccess) return Result<WalletWorkflowResponse>.Failure(load.Message!, load.StatusCode);

        var withdrawal = load.Data!.Entity;
        if (!allowedStatuses.Contains(withdrawal.WorkflowStatus))
        {
            return Result<WalletWorkflowResponse>.Failure(
                $"Cannot move withdrawal from {withdrawal.WorkflowStatus} to {workflowStatus}",
                400);
        }

        Guid? actorCredentialId = ResolveActor(load.Data.Context, withdrawal.CredentialId);
        WalletApprovalRequest? approval = null;
        if (workflowStatus is WalletWorkflowStatus.Rejected)
        {
            if (IsTrustedProviderTerminalDecision(load.Data.Context, request))
            {
                actorCredentialId = load.Data.Context.ActorCredentialId;
            }
            else
            {
                var approvalActor = await ResolveApprovalDecisionActorAsync(
                    withdrawal.ApprovalId,
                    withdrawal.TenantId,
                    load.Data.Context,
                    withdrawal.RequestedByCredentialId ?? withdrawal.CredentialId,
                    ct);
                if (!approvalActor.IsSuccess)
                {
                    return Result<WalletWorkflowResponse>.Failure(approvalActor.Message!, approvalActor.StatusCode);
                }

                actorCredentialId = approvalActor.Data;
            }

            approval = await LoadPendingApprovalAsync(withdrawal.ApprovalId, withdrawal.TenantId, ct);
        }

        if (!withdrawal.HoldOperationId.HasValue)
        {
            ApplyWithdrawalTerminalState(withdrawal, workflowStatus, transactionStatus, request.Reason);
            if (workflowStatus is WalletWorkflowStatus.Rejected)
            {
                ApplyApprovalDecision(approval, WalletApprovalStatus.Rejected, actorCredentialId, request.Reason);
            }

            await LinkWebhookAsync(
                request.WebhookEventId,
                withdrawal.TenantId,
                depositRequestId: null,
                withdrawalRequestId: withdrawal.Id,
                operationId: null,
                processingError: null,
                ct);
            await dbContext.SaveChangesAsync(ct);
            return Result<WalletWorkflowResponse>.Success(ToWithdrawalResponse(withdrawal, $"Withdrawal {workflowStatus}"));
        }

        var wallet = await dbContext.Set<Wallet>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == withdrawal.WalletId && x.TenantId == withdrawal.TenantId && !x.IsDeleted, ct);
        if (wallet is null)
        {
            return Result<WalletWorkflowResponse>.NotFound("Wallet not found");
        }

        var heldTransaction = withdrawal.SettlementTransactionId.HasValue
            ? await dbContext.Set<WalletTransaction>()
                .IgnoreQueryFilters()
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == withdrawal.SettlementTransactionId && x.TenantId == withdrawal.TenantId, ct)
            : null;

        var amount = (withdrawal.Amount ?? 0) + (withdrawal.Fee ?? withdrawal.CalculatedFee ?? 0);
        if (amount <= 0)
        {
            return Result<WalletWorkflowResponse>.Failure("Withdrawal hold amount is invalid", 400);
        }

        var postings = new List<WalletLedgerPostingRequest>
        {
            new()
            {
                WalletId = wallet.Id,
                WalletTransactionId = heldTransaction?.Id,
                Direction = WalletLedgerDirection.Credit,
                BalanceBucket = WalletBalanceBucket.DebitHold,
                EntryKind = WalletLedgerEntryKind.Release,
                Amount = amount,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = withdrawal.ReferenceNumber,
                Description = $"Release withdrawal hold for {workflowStatus}"
            },
            new()
            {
                Direction = WalletLedgerDirection.Debit,
                BalanceBucket = WalletBalanceBucket.External,
                EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                Amount = amount,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = withdrawal.ReferenceNumber,
                CounterpartyType = "withdrawal-hold",
                CounterpartyReference = withdrawal.Id.ToString(),
                Description = $"Reverse withdrawal hold counterparty for {workflowStatus}"
            }
        };

        var ledgerResult = await ledgerService.ExecuteAsync(new WalletLedgerExecutionRequest
        {
            TenantId = withdrawal.TenantId,
            OperationType = WalletOperationType.Release,
            ActorCredentialId = actorCredentialId ?? withdrawal.CredentialId,
            IdempotencyKey = request.IdempotencyKey ?? $"withdrawal:{workflowStatus}:release:{withdrawal.Id}",
            ReferenceNumber = withdrawal.ReferenceNumber,
            ExternalReference = request.ExternalReference ?? withdrawal.ExternalReference,
            Reason = request.Reason ?? withdrawal.Remarks,
            ApprovalId = withdrawal.ApprovalId,
            Postings = postings,
            TransactionUpdates = heldTransaction is null
                ? []
                :
                [
                    new WalletTransactionStateUpdateRequest
                    {
                        Transaction = heldTransaction,
                        WalletId = wallet.Id,
                        Held = false,
                        Released = false
                    }
                ],
            BeforeCommitAsync = async (operation, callbackCt) =>
            {
                ApplyWithdrawalTerminalState(withdrawal, workflowStatus, transactionStatus, request.Reason);
                if (workflowStatus is WalletWorkflowStatus.Rejected)
                {
                    ApplyApprovalDecision(approval, WalletApprovalStatus.Rejected, actorCredentialId, request.Reason);
                }

                await LinkWebhookAsync(
                    request.WebhookEventId,
                    withdrawal.TenantId,
                    depositRequestId: null,
                    withdrawalRequestId: withdrawal.Id,
                    operation.Id,
                    processingError: null,
                    callbackCt);
            }
        }, ct);

        if (!ledgerResult.IsSuccess)
        {
            return Result<WalletWorkflowResponse>.Failure(ledgerResult.Message!, ledgerResult.StatusCode);
        }

        return Result<WalletWorkflowResponse>.Success(ToWithdrawalResponse(withdrawal, $"Withdrawal {workflowStatus}"));
    }

    public async Task<Result<int>> ExpireDueAsync(ExpireWalletWorkflowsRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<int>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsPolicy, ct);
        if (!feature.IsSuccess) return Failure<int>(feature);

        var now = DateTime.UtcNow;
        var count = 0;

        if (request.IncludeDeposits)
        {
            var deposits = await dbContext.Set<DepositRequest>()
                .IgnoreQueryFilters()
                .AsTracking()
                .Where(x =>
                    x.TenantId == contextResult.Data!.TenantId &&
                    !x.IsDeleted &&
                    x.ExpiryDate <= now &&
                    x.WorkflowStatus != WalletWorkflowStatus.Completed &&
                    x.WorkflowStatus != WalletWorkflowStatus.Cancelled &&
                    x.WorkflowStatus != WalletWorkflowStatus.Expired)
                .ToListAsync(ct);
            foreach (var deposit in deposits)
            {
                deposit.WorkflowStatus = WalletWorkflowStatus.Expired;
                deposit.DepositStatus = (short)DepositStatus.Expired;
                count++;
            }
        }

        if (request.IncludeWithdrawals)
        {
            var withdrawals = await dbContext.Set<WithdrawalRequest>()
                .IgnoreQueryFilters()
                .AsTracking()
                .Where(x =>
                    x.TenantId == contextResult.Data!.TenantId &&
                    !x.IsDeleted &&
                    x.WorkflowStatus == WalletWorkflowStatus.PendingApproval &&
                    x.CreatedAt < now.AddDays(-1))
                .ToListAsync(ct);
            foreach (var withdrawal in withdrawals)
            {
                withdrawal.WorkflowStatus = WalletWorkflowStatus.Expired;
                withdrawal.WithdrawalStatus = TransactionStatus.Expired;
                count++;
            }
        }

        await dbContext.SaveChangesAsync(ct);
        return Result<int>.Success(count);
    }

    public async Task<Result<WalletApprovalResponse>> CreateAsync(CreateWalletApprovalRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<WalletApprovalResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsPolicy, ct);
        if (!feature.IsSuccess) return Failure<WalletApprovalResponse>(feature);

        if (request.WalletId.HasValue)
        {
            var walletExists = await dbContext.Set<Wallet>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == request.WalletId.Value &&
                    x.TenantId == contextResult.Data!.TenantId &&
                    !x.IsDeleted,
                    ct);
            if (!walletExists)
            {
                return Result<WalletApprovalResponse>.NotFound("Wallet not found");
            }
        }

        var approval = new WalletApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = contextResult.Data!.TenantId,
            OperationType = request.OperationType,
            Status = WalletApprovalStatus.Pending,
            WalletId = request.WalletId,
            RequesterCredentialId = contextResult.Data.ActorCredentialId ?? request.Metadata.CredentialId ?? Guid.Empty,
            Amount = request.Amount,
            Reason = request.Reason,
            AuditMetadataJson = request.AuditMetadataJson,
            RequestedAt = DateTime.UtcNow
        };

        if (approval.RequesterCredentialId == Guid.Empty)
        {
            return Result<WalletApprovalResponse>.Failure("Requester credential is required for maker-checker approval", 400);
        }

        dbContext.Set<WalletApprovalRequest>().Add(approval);
        await dbContext.SaveChangesAsync(ct);

        return Result<WalletApprovalResponse>.Success(new WalletApprovalResponse
        {
            ApprovalId = approval.Id,
            Status = approval.Status,
            Message = "Approval request created"
        });
    }

    public Task<Result<WalletApprovalResponse>> ApproveAsync(WalletApprovalDecisionRequest request, CancellationToken ct = default) =>
        DecideApprovalAsync(request, WalletApprovalStatus.Approved, ct);

    public Task<Result<WalletApprovalResponse>> RejectAsync(WalletApprovalDecisionRequest request, CancellationToken ct = default) =>
        DecideApprovalAsync(request, WalletApprovalStatus.Rejected, ct);

    public async Task<Result<WalletCaseResponse>> CreateAsync(CreateWalletCaseRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<WalletCaseResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsPolicy, ct);
        if (!feature.IsSuccess) return Failure<WalletCaseResponse>(feature);

        var wallet = await dbContext.Set<Wallet>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.WalletId && x.TenantId == contextResult.Data!.TenantId && !x.IsDeleted, ct);
        if (wallet is null)
        {
            return Result<WalletCaseResponse>.NotFound("Wallet not found");
        }

        var authorization = AuthorizeWorkflowActor(contextResult.Data!, wallet.CredentialId);
        if (!authorization.IsSuccess)
        {
            return Result<WalletCaseResponse>.Failure(authorization.Message!, authorization.StatusCode);
        }

        var walletCase = new WalletCase
        {
            Id = Guid.NewGuid(),
            TenantId = contextResult.Data!.TenantId,
            CaseType = request.CaseType,
            Status = WalletCaseStatus.Open,
            WalletId = request.WalletId,
            OriginalOperationId = request.OriginalOperationId,
            OriginalTransactionId = request.OriginalTransactionId,
            Amount = request.Amount,
            ExternalReference = request.ExternalReference,
            ReasonCode = request.ReasonCode,
            Reason = request.Reason,
            RequesterCredentialId = contextResult.Data.ActorCredentialId ?? wallet.CredentialId
        };

        dbContext.Set<WalletCase>().Add(walletCase);
        await dbContext.SaveChangesAsync(ct);

        return Result<WalletCaseResponse>.Success(ToCaseResponse(walletCase, "Wallet case created"));
    }

    public async Task<Result<WalletCaseResponse>> ResolveAsync(ResolveWalletCaseRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<WalletCaseResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsPolicy, ct);
        if (!feature.IsSuccess) return Failure<WalletCaseResponse>(feature);

        var walletCase = await dbContext.Set<WalletCase>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CaseId && x.TenantId == contextResult.Data!.TenantId && !x.IsDeleted, ct);
        if (walletCase is null)
        {
            return Result<WalletCaseResponse>.NotFound("Wallet case not found");
        }

        var wallet = await dbContext.Set<Wallet>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == walletCase.WalletId && x.TenantId == walletCase.TenantId && !x.IsDeleted, ct);
        if (wallet is null)
        {
            return Result<WalletCaseResponse>.NotFound("Wallet not found");
        }

        var authorization = AuthorizeWorkflowActor(contextResult.Data!, wallet.CredentialId);
        if (!authorization.IsSuccess)
        {
            return Result<WalletCaseResponse>.Failure(authorization.Message!, authorization.StatusCode);
        }

        if (!request.Approve)
        {
            walletCase.Status = WalletCaseStatus.Rejected;
            walletCase.DeciderCredentialId = contextResult.Data!.ActorCredentialId;
            walletCase.ResolvedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return Result<WalletCaseResponse>.Success(ToCaseResponse(walletCase, "Wallet case rejected"));
        }

        var credit = walletCase.CaseType is WalletCaseType.Refund;
        var ledgerResult = await ledgerService.ExecuteAsync(new WalletLedgerExecutionRequest
        {
            TenantId = walletCase.TenantId,
            OperationType = walletCase.CaseType switch
            {
                WalletCaseType.Refund => WalletOperationType.Refund,
                WalletCaseType.Dispute => WalletOperationType.DisputeResolution,
                WalletCaseType.Chargeback => WalletOperationType.Chargeback,
                _ => WalletOperationType.Reversal
            },
            ActorCredentialId = contextResult.Data!.ActorCredentialId ?? wallet.CredentialId,
            IdempotencyKey = request.IdempotencyKey ?? $"wallet-case:resolve:{walletCase.Id}",
            ReferenceNumber = walletCase.ExternalReference ?? walletCase.Id.ToString("N"),
            ExternalReference = walletCase.ExternalReference,
            Reason = request.Reason ?? walletCase.Reason,
            OriginalOperationId = walletCase.OriginalOperationId,
            Postings =
            [
                new WalletLedgerPostingRequest
                {
                    Direction = credit ? WalletLedgerDirection.Debit : WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.External,
                    EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                    Amount = walletCase.Amount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = walletCase.ExternalReference,
                    CounterpartyType = "wallet-case",
                    CounterpartyReference = walletCase.Id.ToString(),
                    Description = "Wallet case counterparty"
                },
                new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    Direction = credit ? WalletLedgerDirection.Credit : WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.Available,
                    EntryKind = walletCase.CaseType switch
                    {
                        WalletCaseType.Refund => WalletLedgerEntryKind.Refund,
                        WalletCaseType.Dispute => WalletLedgerEntryKind.Dispute,
                        WalletCaseType.Chargeback => WalletLedgerEntryKind.Chargeback,
                        _ => WalletLedgerEntryKind.Reversal
                    },
                    Amount = walletCase.Amount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = walletCase.ExternalReference,
                    Description = $"{walletCase.CaseType} settlement"
                }
            ],
            BeforeCommitAsync = (operation, _) =>
            {
                walletCase.Status = WalletCaseStatus.Resolved;
                walletCase.SettlementOperationId = operation.Id;
                walletCase.DeciderCredentialId = contextResult.Data.ActorCredentialId;
                walletCase.ResolvedAt = DateTime.UtcNow;
                return Task.CompletedTask;
            }
        }, ct);

        if (!ledgerResult.IsSuccess)
        {
            return Result<WalletCaseResponse>.Failure(ledgerResult.Message!, ledgerResult.StatusCode);
        }

        return Result<WalletCaseResponse>.Success(ToCaseResponse(walletCase, "Wallet case resolved"));
    }

    public async Task<Result<List<WalletStatementLineResponse>>> GetStatementAsync(WalletStatementRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<List<WalletStatementLineResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsReporting, ct);
        if (!feature.IsSuccess) return Failure<List<WalletStatementLineResponse>>(feature);
        var scope = await ResolveReportWalletScopeAsync(contextResult.Data!, request.WalletId, ct);
        if (!scope.IsSuccess) return Result<List<WalletStatementLineResponse>>.Failure(scope.Message!, scope.StatusCode);

        var query = dbContext.Set<WalletLedgerEntry>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == contextResult.Data!.TenantId && !x.IsDeleted && x.WalletId == request.WalletId);

        if (request.From.HasValue) query = query.Where(x => x.CreatedAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(x => x.CreatedAt <= request.To.Value);
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber)) query = query.Where(x => x.ReferenceNumber == request.ReferenceNumber);

        var lines = await query
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Sequence)
            .Select(x => new WalletStatementLineResponse
            {
                CreatedAt = x.CreatedAt,
                OperationId = x.OperationId,
                LedgerEntryId = x.Id,
                TransactionId = x.WalletTransactionId,
                OperationType = x.Operation.OperationType,
                Direction = x.Direction,
                BalanceBucket = x.BalanceBucket,
                Amount = x.Amount,
                RunningBalance = x.RunningBalance,
                ReferenceNumber = x.ReferenceNumber,
                Description = x.Description
            })
            .ToListAsync(ct);

        return Result<List<WalletStatementLineResponse>>.Success(lines);
    }

    public async Task<Result<List<WalletStatementLineResponse>>> GetLedgerEntriesAsync(WalletLedgerEntriesRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<List<WalletStatementLineResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsReporting, ct);
        if (!feature.IsSuccess) return Failure<List<WalletStatementLineResponse>>(feature);
        var scope = await ResolveReportWalletScopeAsync(contextResult.Data!, request.WalletId, ct);
        if (!scope.IsSuccess) return Result<List<WalletStatementLineResponse>>.Failure(scope.Message!, scope.StatusCode);

        var query = dbContext.Set<WalletLedgerEntry>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == contextResult.Data!.TenantId && !x.IsDeleted);

        if (request.WalletId.HasValue) query = query.Where(x => x.WalletId == request.WalletId.Value);
        if (!scope.Data!.TenantWide) query = query.Where(x => x.WalletId.HasValue && scope.Data.WalletIds.Contains(x.WalletId.Value));
        if (request.From.HasValue) query = query.Where(x => x.CreatedAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(x => x.CreatedAt <= request.To.Value);
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber)) query = query.Where(x => x.ReferenceNumber == request.ReferenceNumber);

        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var lines = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Sequence)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WalletStatementLineResponse
            {
                CreatedAt = x.CreatedAt,
                OperationId = x.OperationId,
                LedgerEntryId = x.Id,
                TransactionId = x.WalletTransactionId,
                OperationType = x.Operation.OperationType,
                Direction = x.Direction,
                BalanceBucket = x.BalanceBucket,
                Amount = x.Amount,
                RunningBalance = x.RunningBalance,
                ReferenceNumber = x.ReferenceNumber,
                Description = x.Description
            })
            .ToListAsync(ct);

        return Result<List<WalletStatementLineResponse>>.Success(lines);
    }

    public async Task<Result<WalletBalanceAsOfResponse>> GetBalanceAsOfAsync(WalletBalanceAsOfRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<WalletBalanceAsOfResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsReporting, ct);
        if (!feature.IsSuccess) return Failure<WalletBalanceAsOfResponse>(feature);
        var scope = await ResolveReportWalletScopeAsync(contextResult.Data!, request.WalletId, ct);
        if (!scope.IsSuccess) return Result<WalletBalanceAsOfResponse>.Failure(scope.Message!, scope.StatusCode);

        var entry = await dbContext.Set<WalletLedgerEntry>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == contextResult.Data!.TenantId &&
                !x.IsDeleted &&
                x.WalletId == request.WalletId &&
                x.CreatedAt <= request.AsOf)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Sequence)
            .FirstOrDefaultAsync(ct);

        if (entry is null)
        {
            var wallet = await dbContext.Set<Wallet>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.WalletId && x.TenantId == contextResult.Data!.TenantId && !x.IsDeleted, ct);
            if (wallet is null) return Result<WalletBalanceAsOfResponse>.NotFound("Wallet not found");
            return Result<WalletBalanceAsOfResponse>.Success(new WalletBalanceAsOfResponse
            {
                WalletId = wallet.Id,
                AsOf = request.AsOf,
                Balance = 0,
                AvailableBalance = 0,
                DebitOnHoldBalance = 0,
                CreditOnHoldBalance = 0
            });
        }

        return Result<WalletBalanceAsOfResponse>.Success(new WalletBalanceAsOfResponse
        {
            WalletId = request.WalletId,
            AsOf = request.AsOf,
            Balance = entry.RunningBalance ?? 0,
            AvailableBalance = entry.RunningAvailableBalance ?? 0,
            DebitOnHoldBalance = entry.RunningDebitOnHoldBalance ?? 0,
            CreditOnHoldBalance = entry.RunningCreditOnHoldBalance ?? 0
        });
    }

    public async Task<Result<List<WalletOperationHistoryResponse>>> GetOperationHistoryAsync(WalletOperationHistoryRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<List<WalletOperationHistoryResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsReporting, ct);
        if (!feature.IsSuccess) return Failure<List<WalletOperationHistoryResponse>>(feature);
        if (!contextResult.Data!.IsPrivilegedActor &&
            request.ActorCredentialId.HasValue &&
            request.ActorCredentialId != contextResult.Data.ActorCredentialId)
        {
            return Result<List<WalletOperationHistoryResponse>>.Forbidden("Actor cannot access another credential's wallet operation history");
        }

        var scope = await ResolveReportWalletScopeAsync(contextResult.Data, request.WalletId, ct);
        if (!scope.IsSuccess) return Result<List<WalletOperationHistoryResponse>>.Failure(scope.Message!, scope.StatusCode);

        var query = dbContext.Set<WalletOperation>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == contextResult.Data!.TenantId && !x.IsDeleted);

        if (request.WalletId.HasValue) query = query.Where(x => x.LedgerEntries.Any(entry => entry.WalletId == request.WalletId.Value));
        if (!scope.Data!.TenantWide) query = query.Where(x => x.LedgerEntries.Any(entry => entry.WalletId.HasValue && scope.Data.WalletIds.Contains(entry.WalletId.Value)));
        if (request.ActorCredentialId.HasValue) query = query.Where(x => x.ActorCredentialId == request.ActorCredentialId.Value);
        if (request.OperationType.HasValue) query = query.Where(x => x.OperationType == request.OperationType.Value);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber)) query = query.Where(x => x.ReferenceNumber == request.ReferenceNumber);
        if (request.From.HasValue) query = query.Where(x => x.CreatedAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(x => x.CreatedAt <= request.To.Value);

        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WalletOperationHistoryResponse
            {
                OperationId = x.Id,
                OperationType = x.OperationType,
                Status = x.Status,
                WalletId = x.LedgerEntries
                    .Where(entry => entry.WalletId.HasValue)
                    .OrderBy(entry => entry.Sequence)
                    .Select(entry => entry.WalletId)
                    .FirstOrDefault(),
                ActorCredentialId = x.ActorCredentialId,
                ReferenceNumber = x.ReferenceNumber,
                ExternalReference = x.ExternalReference,
                RequestedFee = x.RequestedFee,
                CalculatedFee = x.CalculatedFee,
                RequiresApproval = x.RequiresApproval,
                FailureMessage = x.FailureMessage,
                CreatedAt = x.CreatedAt,
                CompletedAt = x.CompletedAt
            })
            .ToListAsync(ct);

        return Result<List<WalletOperationHistoryResponse>>.Success(rows);
    }

    public async Task<Result<List<WalletOperationHistoryResponse>>> GetFailedRejectedOperationsAsync(WalletFailedRejectedOperationsRequest request, CancellationToken ct = default)
    {
        var historyRequest = new WalletOperationHistoryRequest
        {
            WalletId = request.WalletId,
            OperationType = request.OperationType,
            From = request.From,
            To = request.To,
            Page = request.Page,
            PageSize = request.PageSize,
            Metadata = request.Metadata
        };

        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<List<WalletOperationHistoryResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsReporting, ct);
        if (!feature.IsSuccess) return Failure<List<WalletOperationHistoryResponse>>(feature);
        var scope = await ResolveReportWalletScopeAsync(contextResult.Data!, historyRequest.WalletId, ct);
        if (!scope.IsSuccess) return Result<List<WalletOperationHistoryResponse>>.Failure(scope.Message!, scope.StatusCode);

        var query = dbContext.Set<WalletOperation>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == contextResult.Data!.TenantId &&
                !x.IsDeleted &&
                (x.Status == WalletOperationStatus.Failed || x.Status == WalletOperationStatus.Rejected));

        if (historyRequest.WalletId.HasValue) query = query.Where(x => x.LedgerEntries.Any(entry => entry.WalletId == historyRequest.WalletId.Value));
        if (!scope.Data!.TenantWide) query = query.Where(x => x.LedgerEntries.Any(entry => entry.WalletId.HasValue && scope.Data.WalletIds.Contains(entry.WalletId.Value)));
        if (historyRequest.OperationType.HasValue) query = query.Where(x => x.OperationType == historyRequest.OperationType.Value);
        if (historyRequest.From.HasValue) query = query.Where(x => x.CreatedAt >= historyRequest.From.Value);
        if (historyRequest.To.HasValue) query = query.Where(x => x.CreatedAt <= historyRequest.To.Value);

        var page = NormalizePage(historyRequest.Page);
        var pageSize = NormalizePageSize(historyRequest.PageSize);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WalletOperationHistoryResponse
            {
                OperationId = x.Id,
                OperationType = x.OperationType,
                Status = x.Status,
                WalletId = x.LedgerEntries
                    .Where(entry => entry.WalletId.HasValue)
                    .OrderBy(entry => entry.Sequence)
                    .Select(entry => entry.WalletId)
                    .FirstOrDefault(),
                ActorCredentialId = x.ActorCredentialId,
                ReferenceNumber = x.ReferenceNumber,
                ExternalReference = x.ExternalReference,
                RequestedFee = x.RequestedFee,
                CalculatedFee = x.CalculatedFee,
                RequiresApproval = x.RequiresApproval,
                FailureMessage = x.FailureMessage,
                CreatedAt = x.CreatedAt,
                CompletedAt = x.CompletedAt
            })
            .ToListAsync(ct);

        return Result<List<WalletOperationHistoryResponse>>.Success(rows);
    }

    public async Task<Result<List<WalletReconciliationItemResponse>>> GetUnreconciledBalancesAsync(WalletUnreconciledBalancesRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<List<WalletReconciliationItemResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsReporting, ct);
        if (!feature.IsSuccess) return Failure<List<WalletReconciliationItemResponse>>(feature);
        var scope = await ResolveReportWalletScopeAsync(contextResult.Data!, request.WalletId, ct);
        if (!scope.IsSuccess) return Result<List<WalletReconciliationItemResponse>>.Failure(scope.Message!, scope.StatusCode);

        var query = dbContext.Set<WalletReconciliationItem>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == contextResult.Data!.TenantId &&
                !x.IsDeleted &&
                x.Status != WalletReconciliationStatus.Matched &&
                x.Status != WalletReconciliationStatus.MarkedReconciled);

        if (request.WalletId.HasValue) query = query.Where(x => x.WalletId == request.WalletId.Value);
        if (!scope.Data!.TenantWide) query = query.Where(x => x.WalletId.HasValue && scope.Data.WalletIds.Contains(x.WalletId.Value));

        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WalletReconciliationItemResponse
            {
                Id = x.Id,
                WalletId = x.WalletId,
                CheckType = x.CheckType,
                Status = x.Status,
                ExpectedAmount = x.ExpectedAmount,
                ActualAmount = x.ActualAmount,
                DriftAmount = x.DriftAmount,
                RepairSuggestion = x.RepairSuggestion
            })
            .ToListAsync(ct);

        return Result<List<WalletReconciliationItemResponse>>.Success(rows);
    }

    public async Task<Result<List<WalletOutboxFailureResponse>>> GetOutboxFailuresAsync(WalletOutboxFailuresRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<List<WalletOutboxFailureResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsReporting, ct);
        if (!feature.IsSuccess) return Failure<List<WalletOutboxFailureResponse>>(feature);
        if (!contextResult.Data!.IsPrivilegedActor)
        {
            return Result<List<WalletOutboxFailureResponse>>.Forbidden("Outbox failure reporting requires privileged wallet access");
        }

        var query = dbContext.Set<WalletOutboxMessage>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == contextResult.Data!.TenantId &&
                !x.IsDeleted &&
                (x.Status == WalletOutboxStatus.Failed || x.Status == WalletOutboxStatus.DeadLetter));

        if (request.From.HasValue) query = query.Where(x => x.CreatedAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(x => x.CreatedAt <= request.To.Value);

        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WalletOutboxFailureResponse
            {
                OutboxMessageId = x.Id,
                AggregateId = x.AggregateId,
                AggregateType = x.AggregateType,
                EventType = x.EventType,
                Status = x.Status,
                Attempts = x.Attempts,
                NextAttemptAt = x.NextAttemptAt,
                PublishedAt = x.PublishedAt,
                LastError = x.LastError,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return Result<List<WalletOutboxFailureResponse>>.Success(rows);
    }

    public async Task<Result<List<WalletSettlementReportResponse>>> GetSettlementReportAsync(WalletSettlementReportRequest request, CancellationToken ct = default)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<List<WalletSettlementReportResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsReporting, ct);
        if (!feature.IsSuccess) return Failure<List<WalletSettlementReportResponse>>(feature);
        var scope = await ResolveReportWalletScopeAsync(contextResult.Data!, requestedWalletId: null, ct);
        if (!scope.IsSuccess) return Result<List<WalletSettlementReportResponse>>.Failure(scope.Message!, scope.StatusCode);
        var context = contextResult.Data!;
        var reportScope = scope.Data!;

        var rows = new List<WalletSettlementReportResponse>();
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var skip = (page - 1) * pageSize;

        if (request.IncludeDeposits)
        {
            var deposits = dbContext.Set<DepositRequest>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.TenantId == context.TenantId && !x.IsDeleted);

            if (!reportScope.TenantWide && context.ActorCredentialId.HasValue)
            {
                deposits = deposits.Where(x => x.CredentialId == context.ActorCredentialId.Value);
            }
            if (request.Status.HasValue) deposits = deposits.Where(x => x.WorkflowStatus == request.Status.Value);
            if (request.From.HasValue) deposits = deposits.Where(x => x.CreatedAt >= request.From.Value);
            if (request.To.HasValue) deposits = deposits.Where(x => x.CreatedAt <= request.To.Value);

            rows.AddRange(await deposits
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new WalletSettlementReportResponse
                {
                    WorkflowType = "deposit",
                    RequestId = x.Id,
                    WalletId = x.WalletId,
                    CredentialId = x.CredentialId,
                    Status = x.WorkflowStatus,
                    Amount = x.Amount ?? 0,
                    RequestedFee = x.RequestedFee ?? 0,
                    CalculatedFee = x.CalculatedFee ?? 0,
                    ReferenceNumber = x.ReferenceNo,
                    ExternalReference = x.ExternalReference,
                    ProviderStatus = x.ProviderStatus,
                    SettlementOperationId = x.SettlementOperationId,
                    SettlementTransactionId = x.SettlementTransactionId,
                    CreatedAt = x.CreatedAt,
                    SettledAt = x.SettledAt
                })
                .ToListAsync(ct));
        }

        if (request.IncludeWithdrawals)
        {
            var withdrawals = dbContext.Set<WithdrawalRequest>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.TenantId == context.TenantId && !x.IsDeleted);

            if (!reportScope.TenantWide && context.ActorCredentialId.HasValue)
            {
                withdrawals = withdrawals.Where(x => x.CredentialId == context.ActorCredentialId.Value);
            }
            if (request.Status.HasValue) withdrawals = withdrawals.Where(x => x.WorkflowStatus == request.Status.Value);
            if (request.From.HasValue) withdrawals = withdrawals.Where(x => x.CreatedAt >= request.From.Value);
            if (request.To.HasValue) withdrawals = withdrawals.Where(x => x.CreatedAt <= request.To.Value);

            rows.AddRange(await withdrawals
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new WalletSettlementReportResponse
                {
                    WorkflowType = "withdrawal",
                    RequestId = x.Id,
                    WalletId = x.WalletId,
                    CredentialId = x.CredentialId,
                    Status = x.WorkflowStatus,
                    Amount = x.Amount ?? 0,
                    RequestedFee = x.RequestedFee ?? 0,
                    CalculatedFee = x.CalculatedFee ?? 0,
                    ReferenceNumber = x.ReferenceNumber,
                    ExternalReference = x.ExternalReference,
                    ProviderStatus = x.ProviderStatus,
                    SettlementOperationId = x.SettlementOperationId,
                    SettlementTransactionId = x.SettlementTransactionId,
                    CreatedAt = x.CreatedAt,
                    SettledAt = x.SettledAt
                })
                .ToListAsync(ct));
        }

        return Result<List<WalletSettlementReportResponse>>.Success(
            rows
                .OrderByDescending(x => x.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .ToList());
    }

    private static int NormalizePage(int page) => page <= 0 ? 1 : page;

    private static int NormalizePageSize(int pageSize) => Math.Clamp(pageSize, 1, 500);

    private async Task<Result> TryInitiateDepositProviderAsync(DepositRequest deposit, CancellationToken ct)
    {
        var gateway = await LoadPaymentGatewayAsync(deposit.TenantId, deposit.GatewayId, ct);
        if (gateway is null)
        {
            return Result.Success();
        }

        if (paymentGatewayService.GetProvider(gateway) is null)
        {
            if (RequireRegisteredPaymentProvider())
            {
                deposit.WorkflowStatus = WalletWorkflowStatus.Failed;
                deposit.DepositStatus = (short)DepositStatus.InvalidPayment;
                deposit.FailedAt = DateTime.UtcNow;
                deposit.FailureReason = "Payment provider is not registered";
                await dbContext.SaveChangesAsync(ct);
                return Result.Failure("Payment provider is not registered", 400);
            }

            return Result.Success();
        }

        var response = await paymentGatewayService.ProcessCashInAsync(new CreateCashInRequest
        {
            PaymentGateway = gateway,
            Amount = deposit.Amount ?? 0,
            ReferenceNumber = deposit.ReferenceNo,
            Description = deposit.Remarks,
            MerchantId = GetPaymentMerchantId(),
            SourceAccountNumber = deposit.Address,
            PaymentMethod = "wallet-deposit"
        }, ct);

        ApplyProviderResponse(deposit, response);
        if (!response.Success && RequireRegisteredPaymentProvider())
        {
            deposit.WorkflowStatus = WalletWorkflowStatus.Failed;
            deposit.DepositStatus = (short)DepositStatus.InvalidPayment;
            deposit.FailedAt = DateTime.UtcNow;
            deposit.FailureReason = "Payment provider rejected the deposit initiation";
            await dbContext.SaveChangesAsync(ct);
            return Result.Failure("Payment provider rejected the deposit initiation", 400);
        }

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result<WalletWorkflowResponse>?> TryInitiateWithdrawalProviderAsync(
        WithdrawalRequest withdrawal,
        WalletWorkflowActionRequest sourceRequest,
        CancellationToken ct)
    {
        if (!withdrawal.GatewayId.HasValue)
        {
            return null;
        }

        var gateway = await LoadPaymentGatewayAsync(withdrawal.TenantId, withdrawal.GatewayId.Value, ct);
        if (gateway is null)
        {
            return null;
        }

        if (paymentGatewayService.GetProvider(gateway) is null)
        {
            if (!RequireRegisteredPaymentProvider())
            {
                return null;
            }

            return await FailWithdrawalForProviderAsync(
                withdrawal,
                sourceRequest,
                "Payment provider is not registered",
                null,
                ct);
        }

        var response = await paymentGatewayService.ProcessCashOutAsync(new CreateCashoutRequest
        {
            PaymentGateway = gateway,
            Amount = withdrawal.Amount ?? 0,
            ReferenceNumber = withdrawal.ReferenceNumber,
            Description = withdrawal.Remarks,
            MerchantId = GetPaymentMerchantId()
        }, ct);

        if (response.Success)
        {
            return await SettleWithdrawalAsync(new WalletWorkflowActionRequest
            {
                RequestId = withdrawal.Id,
                ProviderEventId = response.ReferenceId,
                ProviderTransactionId = response.ReferenceId,
                ProviderStatus = ToProviderStatus(response),
                ExternalReference = withdrawal.ExternalReference,
                RawProviderPayloadJson = SerializeProviderResponse(response),
                IdempotencyKey = $"withdrawal:provider:settle:{withdrawal.Id}:{response.ReferenceId ?? withdrawal.ReferenceNumber}",
                Metadata = sourceRequest.Metadata
            }, ct);
        }

        return await FailWithdrawalForProviderAsync(
            withdrawal,
            sourceRequest,
            "Payment provider rejected the payout",
            response,
            ct);
    }

    private async Task<Result<WalletWorkflowResponse>> FailWithdrawalForProviderAsync(
        WithdrawalRequest withdrawal,
        WalletWorkflowActionRequest sourceRequest,
        string reason,
        PaymentResponse? response,
        CancellationToken ct)
    {
        return await FailWithdrawalAsync(new WalletWorkflowActionRequest
        {
            RequestId = withdrawal.Id,
            Reason = reason,
            ProviderEventId = response?.ReferenceId,
            ProviderTransactionId = response?.ReferenceId,
            ProviderStatus = response is null ? null : ToProviderStatus(response),
            ExternalReference = withdrawal.ExternalReference,
            RawProviderPayloadJson = response is null ? null : SerializeProviderResponse(response),
            IdempotencyKey = $"withdrawal:provider:fail:{withdrawal.Id}:{response?.ReferenceId ?? withdrawal.ReferenceNumber}",
            Metadata = sourceRequest.Metadata
        }, ct);
    }

    private async Task<PaymentGateway?> LoadPaymentGatewayAsync(Guid tenantId, Guid gatewayId, CancellationToken ct)
    {
        return await dbContext.Set<PaymentGateway>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == gatewayId && x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled, ct);
    }

    private static void ApplyProviderResponse(DepositRequest deposit, PaymentResponse response)
    {
        deposit.ProviderTransactionId = response.ReferenceId ?? deposit.ProviderTransactionId;
        deposit.ProviderStatus = ToProviderStatus(response);
        deposit.RawResponseData = SerializeProviderResponse(response);
    }

    private static string ToProviderStatus(PaymentResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.ProviderResponseCode) ||
            !string.IsNullOrWhiteSpace(response.ProviderResponseMessage))
        {
            return string.Join(
                ':',
                new[] { response.ProviderResponseCode, response.ProviderResponseMessage }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        return response.Success ? "approved" : "failed";
    }

    private static string SerializeProviderResponse(PaymentResponse response)
    {
        var json = JsonSerializer.Serialize(response);
        return json.Length <= 5000 ? json : json[..5000];
    }

    private string GetPaymentMerchantId() =>
        configuration["Wallets:Payments:MerchantId"]
        ?? configuration["Payments:MerchantId"]
        ?? "merchant-123";

    private bool RequireRegisteredPaymentProvider() =>
        configuration.GetValue("Wallets:Payments:RequireRegisteredProvider", false)
        || configuration.GetValue("Payments:RequireRegisteredProvider", false);

    private async Task<Result<WalletWorkflowResponse>> TransitionDepositAsync(
        WalletWorkflowActionRequest request,
        IReadOnlyCollection<WalletWorkflowStatus> allowedStatuses,
        WalletWorkflowStatus nextStatus,
        CancellationToken ct,
        bool approve = false,
        bool reject = false,
        bool fail = false,
        bool cancel = false)
    {
        var load = await LoadDepositAsync(request, true, ct);
        if (!load.IsSuccess) return Result<WalletWorkflowResponse>.Failure(load.Message!, load.StatusCode);

        var deposit = load.Data!.Entity;
        if (!allowedStatuses.Contains(deposit.WorkflowStatus))
        {
            return Result<WalletWorkflowResponse>.Failure($"Cannot move deposit from {deposit.WorkflowStatus} to {nextStatus}", 400);
        }

        deposit.WorkflowStatus = nextStatus;
        deposit.Remarks = request.Reason ?? deposit.Remarks;
        if (approve)
        {
            var approvalActor = await ResolveApprovalDecisionActorAsync(
                deposit.ApprovalId,
                deposit.TenantId,
                load.Data.Context,
                deposit.RequestedByCredentialId ?? deposit.CredentialId,
                ct);
            if (!approvalActor.IsSuccess)
            {
                return Result<WalletWorkflowResponse>.Failure(approvalActor.Message!, approvalActor.StatusCode);
            }

            deposit.ApprovedByCredentialId = approvalActor.Data;
            deposit.ApprovedAt = DateTime.UtcNow;
            deposit.DepositStatus = (short)DepositStatus.PendingPayment;
            await UpdateApprovalAsync(deposit.ApprovalId, deposit.TenantId, WalletApprovalStatus.Approved, deposit.ApprovedByCredentialId, request.Reason, ct);
        }
        if (reject)
        {
            Guid? decisionActorId = load.Data.Context.ActorCredentialId;
            if (!IsTrustedProviderTerminalDecision(load.Data.Context, request))
            {
                var approvalActor = await ResolveApprovalDecisionActorAsync(
                    deposit.ApprovalId,
                    deposit.TenantId,
                    load.Data.Context,
                    deposit.RequestedByCredentialId ?? deposit.CredentialId,
                    ct);
                if (!approvalActor.IsSuccess)
                {
                    return Result<WalletWorkflowResponse>.Failure(approvalActor.Message!, approvalActor.StatusCode);
                }

                decisionActorId = approvalActor.Data;
            }

            deposit.DepositStatus = (short)DepositStatus.Revoked;
            deposit.FailureReason = request.Reason;
            await UpdateApprovalAsync(deposit.ApprovalId, deposit.TenantId, WalletApprovalStatus.Rejected, decisionActorId, request.Reason, ct);
        }
        if (fail)
        {
            deposit.DepositStatus = (short)DepositStatus.InvalidPayment;
            deposit.FailedAt = DateTime.UtcNow;
            deposit.FailureReason = request.Reason;
        }
        if (cancel)
        {
            deposit.DepositStatus = (short)DepositStatus.Cancelled;
            deposit.CancelledAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);
        return Result<WalletWorkflowResponse>.Success(ToDepositResponse(deposit, $"Deposit {nextStatus}"));
    }

    private async Task<Result<WalletWorkflowResponse>> TransitionWithdrawalAsync(
        WalletWorkflowActionRequest request,
        IReadOnlyCollection<WalletWorkflowStatus> allowedStatuses,
        WalletWorkflowStatus nextStatus,
        CancellationToken ct,
        bool approve = false,
        bool reject = false,
        bool fail = false,
        bool cancel = false)
    {
        var load = await LoadWithdrawalAsync(request, true, ct);
        if (!load.IsSuccess) return Result<WalletWorkflowResponse>.Failure(load.Message!, load.StatusCode);

        var withdrawal = load.Data!.Entity;
        if (!allowedStatuses.Contains(withdrawal.WorkflowStatus))
        {
            return Result<WalletWorkflowResponse>.Failure($"Cannot move withdrawal from {withdrawal.WorkflowStatus} to {nextStatus}", 400);
        }

        withdrawal.WorkflowStatus = nextStatus;
        withdrawal.Remarks = request.Reason ?? withdrawal.Remarks;
        if (approve)
        {
            var approvalActor = await ResolveApprovalDecisionActorAsync(
                withdrawal.ApprovalId,
                withdrawal.TenantId,
                load.Data.Context,
                withdrawal.RequestedByCredentialId ?? withdrawal.CredentialId,
                ct);
            if (!approvalActor.IsSuccess)
            {
                return Result<WalletWorkflowResponse>.Failure(approvalActor.Message!, approvalActor.StatusCode);
            }

            withdrawal.ApprovedByCredentialId = approvalActor.Data;
            withdrawal.ApprovedAt = DateTime.UtcNow;
            withdrawal.WithdrawalStatus = TransactionStatus.Accepted;
            await UpdateApprovalAsync(withdrawal.ApprovalId, withdrawal.TenantId, WalletApprovalStatus.Approved, withdrawal.ApprovedByCredentialId, request.Reason, ct);
        }
        if (reject)
        {
            Guid? decisionActorId = load.Data.Context.ActorCredentialId;
            if (!IsTrustedProviderTerminalDecision(load.Data.Context, request))
            {
                var approvalActor = await ResolveApprovalDecisionActorAsync(
                    withdrawal.ApprovalId,
                    withdrawal.TenantId,
                    load.Data.Context,
                    withdrawal.RequestedByCredentialId ?? withdrawal.CredentialId,
                    ct);
                if (!approvalActor.IsSuccess)
                {
                    return Result<WalletWorkflowResponse>.Failure(approvalActor.Message!, approvalActor.StatusCode);
                }

                decisionActorId = approvalActor.Data;
            }

            withdrawal.WithdrawalStatus = TransactionStatus.Rejected;
            withdrawal.FailureReason = request.Reason;
            await UpdateApprovalAsync(withdrawal.ApprovalId, withdrawal.TenantId, WalletApprovalStatus.Rejected, decisionActorId, request.Reason, ct);
        }
        if (fail)
        {
            withdrawal.WithdrawalStatus = TransactionStatus.Failed;
            withdrawal.FailedAt = DateTime.UtcNow;
            withdrawal.FailureReason = request.Reason;
        }
        if (cancel)
        {
            withdrawal.WithdrawalStatus = TransactionStatus.Cancelled;
            withdrawal.CancelledAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);
        return Result<WalletWorkflowResponse>.Success(ToWithdrawalResponse(withdrawal, $"Withdrawal {nextStatus}"));
    }

    private sealed record WorkflowLoad<T>(T Entity, WalletRequestContext Context);

    private sealed record WalletReportScope(bool TenantWide, IReadOnlyList<Guid> WalletIds);

    private async Task<Result<WorkflowLoad<DepositRequest>>> LoadDepositAsync(WalletWorkflowActionRequest request, bool tracking, CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<WorkflowLoad<DepositRequest>>.Failure(contextResult.Message!, contextResult.StatusCode);
        var context = contextResult.Data!;
        var feature = await EnsureFeatureAsync(context, TenantModuleFeatureKeys.WalletsDeposits, ct);
        if (!feature.IsSuccess) return Failure<WorkflowLoad<DepositRequest>>(feature);

        var query = dbContext.Set<DepositRequest>().IgnoreQueryFilters();
        query = tracking ? query.AsTracking() : query.AsNoTracking();
        var deposit = await query.FirstOrDefaultAsync(x =>
            x.Id == request.RequestId &&
            x.TenantId == context.TenantId &&
            !x.IsDeleted,
            ct);

        if (deposit is null)
        {
            return Result<WorkflowLoad<DepositRequest>>.NotFound("Deposit request not found");
        }

        var authorization = AuthorizeWorkflowActor(context, deposit.CredentialId);
        return authorization.IsSuccess
            ? Result<WorkflowLoad<DepositRequest>>.Success(new WorkflowLoad<DepositRequest>(deposit, context))
            : Result<WorkflowLoad<DepositRequest>>.Failure(authorization.Message!, authorization.StatusCode);
    }

    private async Task<Result<WorkflowLoad<WithdrawalRequest>>> LoadWithdrawalAsync(WalletWorkflowActionRequest request, bool tracking, CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<WorkflowLoad<WithdrawalRequest>>.Failure(contextResult.Message!, contextResult.StatusCode);
        var context = contextResult.Data!;
        var feature = await EnsureFeatureAsync(context, TenantModuleFeatureKeys.WalletsWithdrawals, ct);
        if (!feature.IsSuccess) return Failure<WorkflowLoad<WithdrawalRequest>>(feature);

        var query = dbContext.Set<WithdrawalRequest>().IgnoreQueryFilters();
        query = tracking ? query.AsTracking() : query.AsNoTracking();
        var withdrawal = await query.FirstOrDefaultAsync(x =>
            x.Id == request.RequestId &&
            x.TenantId == context.TenantId &&
            !x.IsDeleted,
            ct);

        if (withdrawal is null)
        {
            return Result<WorkflowLoad<WithdrawalRequest>>.NotFound("Withdrawal request not found");
        }

        var authorization = AuthorizeWorkflowActor(context, withdrawal.CredentialId);
        return authorization.IsSuccess
            ? Result<WorkflowLoad<WithdrawalRequest>>.Success(new WorkflowLoad<WithdrawalRequest>(withdrawal, context))
            : Result<WorkflowLoad<WithdrawalRequest>>.Failure(authorization.Message!, authorization.StatusCode);
    }

    private async Task<Result<Wallet>> ResolveDepositWalletAsync(DepositRequest deposit, CancellationToken ct)
    {
        if (deposit.WalletId.HasValue)
        {
            return await ResolveExistingDepositWalletAsync(
                deposit.TenantId,
                deposit.WalletId.Value,
                deposit.CredentialId,
                deposit.WalletTypeId,
                ct);
        }

        if (!deposit.WalletTypeId.HasValue)
        {
            return Result<Wallet>.Failure("Wallet type is required to settle deposit without an existing wallet", 400);
        }

        var existing = await dbContext.Set<Wallet>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == deposit.TenantId &&
                !x.IsDeleted &&
                x.CredentialId == deposit.CredentialId &&
                x.WalletTypeId == deposit.WalletTypeId &&
                x.Status != WalletStatus.Closed,
                ct);
        if (existing is not null)
        {
            return Result<Wallet>.Success(existing);
        }

        return Result<Wallet>.Success(new Wallet
        {
            Id = Guid.NewGuid(),
            TenantId = deposit.TenantId,
            CredentialId = deposit.CredentialId,
            WalletTypeId = deposit.WalletTypeId,
            AccountNumber = $"{Random.Shared.NextInt64(1000_0000_0000, 9999_9999_9999)}",
            Status = WalletStatus.Active,
            IsEnabled = true
        });
    }

    private async Task<Result<Wallet>> ResolveExistingDepositWalletAsync(
        Guid tenantId,
        Guid walletId,
        Guid credentialId,
        Guid? walletTypeId,
        CancellationToken ct)
    {
        var wallet = await dbContext.Set<Wallet>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == walletId && x.TenantId == tenantId && !x.IsDeleted, ct);
        if (wallet is null)
        {
            return Result<Wallet>.NotFound("Wallet not found");
        }

        if (credentialId != Guid.Empty && wallet.CredentialId != credentialId)
        {
            return Result<Wallet>.Failure("Wallet does not belong to the requested credential", 400);
        }

        if (walletTypeId.HasValue && wallet.WalletTypeId != walletTypeId)
        {
            return Result<Wallet>.Failure("Wallet does not match requested wallet type", 400);
        }

        return Result<Wallet>.Success(wallet);
    }

    private async Task<Result<WalletApprovalResponse>> DecideApprovalAsync(
        WalletApprovalDecisionRequest request,
        WalletApprovalStatus status,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess) return Result<WalletApprovalResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        var feature = await EnsureFeatureAsync(contextResult.Data!, TenantModuleFeatureKeys.WalletsPolicy, ct);
        if (!feature.IsSuccess) return Failure<WalletApprovalResponse>(feature);

        var approval = await dbContext.Set<WalletApprovalRequest>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ApprovalId && x.TenantId == contextResult.Data!.TenantId && !x.IsDeleted, ct);
        if (approval is null)
        {
            return Result<WalletApprovalResponse>.NotFound("Approval request not found");
        }

        if (approval.Status != WalletApprovalStatus.Pending)
        {
            return Result<WalletApprovalResponse>.Failure("Approval has already been decided", 400);
        }

        if (contextResult.Data!.ActorCredentialId is not { } approverCredentialId || approverCredentialId == Guid.Empty)
        {
            return Result<WalletApprovalResponse>.Failure("Approver credential is required", 400);
        }

        if (approval.RequesterCredentialId == approverCredentialId)
        {
            return Result<WalletApprovalResponse>.Forbidden("Requester cannot approve their own maker-checker request");
        }

        approval.Status = status;
        approval.ApproverCredentialId = approverCredentialId;
        approval.DecisionReason = request.Reason;
        approval.DecidedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return Result<WalletApprovalResponse>.Success(new WalletApprovalResponse
        {
            ApprovalId = approval.Id,
            Status = approval.Status,
            OperationId = approval.OperationId,
            Message = $"Approval {status}"
        });
    }

    private async Task<Result<Guid>> ResolveApprovalDecisionActorAsync(
        Guid? approvalId,
        Guid tenantId,
        WalletRequestContext context,
        Guid requesterCredentialId,
        CancellationToken ct)
    {
        if (context.ActorCredentialId is not { } actorCredentialId || actorCredentialId == Guid.Empty)
        {
            return Result<Guid>.Failure("Approver credential is required", 400);
        }

        if (actorCredentialId == requesterCredentialId)
        {
            return Result<Guid>.Forbidden("Requester cannot approve their own maker-checker request");
        }

        if (!approvalId.HasValue)
        {
            return Result<Guid>.Success(actorCredentialId);
        }

        var approval = await dbContext.Set<WalletApprovalRequest>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == approvalId.Value && x.TenantId == tenantId && !x.IsDeleted, ct);
        if (approval is not null && approval.RequesterCredentialId == actorCredentialId)
        {
            return Result<Guid>.Forbidden("Requester cannot approve their own maker-checker request");
        }

        return Result<Guid>.Success(actorCredentialId);
    }

    private async Task<WalletApprovalRequest?> LoadPendingApprovalAsync(Guid? approvalId, Guid tenantId, CancellationToken ct)
    {
        if (!approvalId.HasValue)
        {
            return null;
        }

        return await dbContext.Set<WalletApprovalRequest>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == approvalId.Value &&
                x.TenantId == tenantId &&
                !x.IsDeleted,
                ct);
    }

    private async Task<Result<WalletReportScope>> ResolveReportWalletScopeAsync(
        WalletRequestContext context,
        Guid? requestedWalletId,
        CancellationToken ct)
    {
        if (context.IsPrivilegedActor)
        {
            return Result<WalletReportScope>.Success(new WalletReportScope(
                TenantWide: !requestedWalletId.HasValue,
                WalletIds: requestedWalletId.HasValue ? [requestedWalletId.Value] : []));
        }

        if (context.ActorCredentialId is not { } actorCredentialId || actorCredentialId == Guid.Empty)
        {
            return Result<WalletReportScope>.Forbidden("Actor credential is required for wallet reports");
        }

        var walletQuery = dbContext.Set<Wallet>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == context.TenantId &&
                !x.IsDeleted &&
                x.CredentialId == actorCredentialId);

        if (requestedWalletId.HasValue)
        {
            walletQuery = walletQuery.Where(x => x.Id == requestedWalletId.Value);
        }

        var walletIds = await walletQuery.Select(x => x.Id).ToListAsync(ct);
        if (requestedWalletId.HasValue && walletIds.Count == 0)
        {
            return Result<WalletReportScope>.Forbidden("Actor cannot access the requested wallet report");
        }

        return Result<WalletReportScope>.Success(new WalletReportScope(false, walletIds));
    }

    private async Task UpdateApprovalAsync(
        Guid? approvalId,
        Guid tenantId,
        WalletApprovalStatus status,
        Guid? actorCredentialId,
        string? reason,
        CancellationToken ct)
    {
        if (!approvalId.HasValue)
        {
            return;
        }

        var approval = await dbContext.Set<WalletApprovalRequest>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == approvalId.Value && x.TenantId == tenantId && !x.IsDeleted, ct);
        if (approval is null || approval.Status != WalletApprovalStatus.Pending)
        {
            return;
        }

        approval.Status = status;
        approval.ApproverCredentialId = actorCredentialId;
        approval.DecisionReason = reason;
        approval.DecidedAt = DateTime.UtcNow;
    }

    private async Task LinkWebhookAsync(
        Guid? webhookEventId,
        Guid tenantId,
        Guid? depositRequestId,
        Guid? withdrawalRequestId,
        Guid? operationId,
        string? processingError,
        CancellationToken ct)
    {
        if (!webhookEventId.HasValue)
        {
            return;
        }

        var webhookSet = dbContext.Set<WalletPaymentWebhookEvent>();
        var webhook = webhookSet.Local.FirstOrDefault(x =>
                x.Id == webhookEventId.Value &&
                x.TenantId == tenantId &&
                !x.IsDeleted)
            ?? await webhookSet
                .IgnoreQueryFilters()
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == webhookEventId.Value && x.TenantId == tenantId && !x.IsDeleted, ct);
        if (webhook is null)
        {
            return;
        }

        webhook.DepositRequestId = depositRequestId ?? webhook.DepositRequestId;
        webhook.WithdrawalRequestId = withdrawalRequestId ?? webhook.WithdrawalRequestId;
        webhook.OperationId = operationId ?? webhook.OperationId;
        webhook.ProcessingStatus = processingError is null
            ? WalletWebhookProcessingStatus.Processed
            : WalletWebhookProcessingStatus.Failed;
        webhook.ProcessingError = processingError;
        webhook.ProcessedAt = DateTime.UtcNow;
    }

    private static void ApplyApprovalDecision(
        WalletApprovalRequest? approval,
        WalletApprovalStatus status,
        Guid? actorCredentialId,
        string? reason)
    {
        if (approval is null || approval.Status != WalletApprovalStatus.Pending)
        {
            return;
        }

        approval.Status = status;
        approval.ApproverCredentialId = actorCredentialId;
        approval.DecisionReason = reason;
        approval.DecidedAt = DateTime.UtcNow;
    }

    private static void ApplyWithdrawalTerminalState(
        WithdrawalRequest withdrawal,
        WalletWorkflowStatus workflowStatus,
        TransactionStatus transactionStatus,
        string? reason)
    {
        withdrawal.WorkflowStatus = workflowStatus;
        withdrawal.WithdrawalStatus = transactionStatus;
        withdrawal.Remarks = reason ?? withdrawal.Remarks;

        if (workflowStatus is WalletWorkflowStatus.Failed)
        {
            withdrawal.FailedAt = DateTime.UtcNow;
            withdrawal.FailureReason = reason;
        }

        if (workflowStatus is WalletWorkflowStatus.Cancelled)
        {
            withdrawal.CancelledAt = DateTime.UtcNow;
        }

        if (workflowStatus is WalletWorkflowStatus.Rejected)
        {
            withdrawal.FailureReason = reason;
        }
    }

    private async Task<Result> EnsureFeatureAsync(WalletRequestContext context, string featureKey, CancellationToken ct) =>
        await featureGateService.EnsureEnabledAsync(context.TenantId, featureKey, ct);

    private static Result<T> Failure<T>(Result result) =>
        Result<T>.Failure(result.Message ?? "Wallet feature check failed", result.StatusCode);

    private static Result AuthorizeWorkflowActor(WalletRequestContext context, Guid ownerCredentialId)
    {
        if (context.IsPrivilegedActor)
        {
            return Result.Success();
        }

        return context.ActorCredentialId == ownerCredentialId
            ? Result.Success()
            : Result.Forbidden("Actor cannot operate on the requested wallet workflow");
    }

    private static bool IsTrustedProviderTerminalDecision(WalletRequestContext context, WalletWorkflowActionRequest request) =>
        context.IsSystemActor &&
        request.WebhookEventId.HasValue &&
        !string.IsNullOrWhiteSpace(request.ProviderStatus);

    private static Guid? ResolveActor(WalletRequestContext context, Guid fallbackCredentialId) =>
        context.ActorCredentialId ?? fallbackCredentialId;

    private static WalletApprovalRequest CreateApproval(
        Guid tenantId,
        WalletOperationType operationType,
        Guid? walletId,
        Guid requesterCredentialId,
        decimal amount,
        string? reason) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OperationType = operationType,
            Status = WalletApprovalStatus.Pending,
            WalletId = walletId,
            RequesterCredentialId = requesterCredentialId,
            Amount = amount,
            Reason = reason,
            RequestedAt = DateTime.UtcNow
        };

    private static WalletWorkflowResponse ToDepositResponse(DepositRequest deposit, string? message = null) =>
        new()
        {
            Id = deposit.Id,
            WalletId = deposit.WalletId,
            OperationType = WalletOperationType.DepositApproval,
            Status = deposit.WorkflowStatus,
            Amount = deposit.Amount ?? 0,
            RequestedFee = deposit.RequestedFee ?? 0,
            CalculatedFee = deposit.CalculatedFee ?? 0,
            ReferenceNumber = deposit.ReferenceNo,
            ExternalReference = deposit.ExternalReference,
            ApprovalId = deposit.ApprovalId,
            OperationId = deposit.SettlementOperationId,
            TransactionId = deposit.SettlementTransactionId,
            Message = message
        };

    private static WalletWorkflowResponse ToWithdrawalResponse(WithdrawalRequest withdrawal, string? message = null) =>
        new()
        {
            Id = withdrawal.Id,
            WalletId = withdrawal.WalletId,
            OperationType = WalletOperationType.WithdrawalApproval,
            Status = withdrawal.WorkflowStatus,
            Amount = withdrawal.Amount ?? 0,
            RequestedFee = withdrawal.RequestedFee ?? 0,
            CalculatedFee = withdrawal.CalculatedFee ?? 0,
            ReferenceNumber = withdrawal.ReferenceNumber,
            ExternalReference = withdrawal.ExternalReference,
            ApprovalId = withdrawal.ApprovalId,
            OperationId = withdrawal.SettlementOperationId,
            TransactionId = withdrawal.SettlementTransactionId,
            Message = message
        };

    private static WalletCaseResponse ToCaseResponse(WalletCase walletCase, string? message = null) =>
        new()
        {
            CaseId = walletCase.Id,
            CaseType = walletCase.CaseType,
            Status = walletCase.Status,
            WalletId = walletCase.WalletId,
            OperationId = walletCase.SettlementOperationId,
            Amount = walletCase.Amount,
            Message = message
        };
}
