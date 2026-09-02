using System.Net;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using Wallets.Domain.Shared.Enums;
using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Portal.Shared;

namespace XFramework.Portal.Features.Finance.Services;

public sealed class WalletsAdminBackendContractService(
    IWalletsServiceWrapper wallets,
    IPortalTenantContext tenantFilter,
    RequestMetadata requestMetadata)
{
    public const string CreateDepositRoute = "POST /api/wallets/deposits";
    public const string ApproveDepositRoute = "POST /api/wallets/deposits/approve";
    public const string RejectDepositRoute = "POST /api/wallets/deposits/reject";
    public const string SettleDepositRoute = "POST /api/wallets/deposits/settle";
    public const string FailDepositRoute = "POST /api/wallets/deposits/fail";
    public const string CancelDepositRoute = "POST /api/wallets/deposits/cancel";
    public const string CreateWithdrawalRoute = "POST /api/wallets/withdrawals";
    public const string ApproveWithdrawalRoute = "POST /api/wallets/withdrawals/approve";
    public const string RejectWithdrawalRoute = "POST /api/wallets/withdrawals/reject";
    public const string SettleWithdrawalRoute = "POST /api/wallets/withdrawals/settle";
    public const string FailWithdrawalRoute = "POST /api/wallets/withdrawals/fail";
    public const string CancelWithdrawalRoute = "POST /api/wallets/withdrawals/cancel";
    public const string BatchIncrementRoute = "POST /api/wallets/batch/increment";
    public const string BatchDecrementRoute = "POST /api/wallets/batch/decrement";
    public const string BatchTransferRoute = "POST /api/wallets/batch/transfer";
    public const string RetryOutboxRoute = "POST /api/wallets/outbox/retry";
    public const string ReplayOutboxRoute = "POST /api/wallets/outbox/replay";
    public const string DeadLetterOutboxRoute = "POST /api/wallets/outbox/dead-letter";
    public const string ReconcileWalletRoute = "POST /api/wallets/reconciliation/run";
    public const string MarkReconciledRoute = "POST /api/wallets/reconciliation/mark-reconciled";
    public const string CreateWalletApprovalRoute = "POST /api/wallets/approvals";
    public const string ApproveWalletApprovalRoute = "POST /api/wallets/approvals/approve";
    public const string RejectWalletApprovalRoute = "POST /api/wallets/approvals/reject";
    public const string SaveWalletPolicyRoute = "POST /api/wallets/policy/rules/upsert";
    public const string SaveWalletFeeScheduleRoute = "POST /api/wallets/policy/fee-schedules/upsert";
    public const string GenerateStatementRoute = "POST /api/wallets/reports/statement";
    public const string CreateWalletCaseRoute = "POST /api/wallets/cases";
    public const string ResolveWalletCaseRoute = "POST /api/wallets/cases/resolve";

    public async Task<CmdResponse> CreateDepositAsync(CreateDepositWorkflowRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.CreateDepositWorkflow(request with
        {
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> ApproveDepositAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.ApproveDepositWorkflow(new ApproveDepositWorkflowRequest
        {
            RequestId = id,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> RejectDepositAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.RejectDepositWorkflow(new RejectDepositWorkflowRequest
        {
            RequestId = id,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> SettleDepositAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.SettleDepositWorkflow(new SettleDepositWorkflowRequest
        {
            RequestId = id,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> FailDepositAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.FailDepositWorkflow(new FailDepositWorkflowRequest
        {
            RequestId = id,
            Reason = "Marked failed from Portal",
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> CancelDepositAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.CancelDepositWorkflow(new CancelDepositWorkflowRequest
        {
            RequestId = id,
            Reason = "Cancelled from Portal",
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> CreateWithdrawalAsync(CreateWithdrawalWorkflowRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.CreateWithdrawalWorkflow(request with
        {
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> ApproveWithdrawalAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.ApproveWithdrawalWorkflow(new ApproveWithdrawalWorkflowRequest
        {
            RequestId = id,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> RejectWithdrawalAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.RejectWithdrawalWorkflow(new RejectWithdrawalWorkflowRequest
        {
            RequestId = id,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> SettleWithdrawalAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.SettleWithdrawalWorkflow(new SettleWithdrawalWorkflowRequest
        {
            RequestId = id,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> FailWithdrawalAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.FailWithdrawalWorkflow(new FailWithdrawalWorkflowRequest
        {
            RequestId = id,
            Reason = "Marked failed from Portal",
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> CancelWithdrawalAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.CancelWithdrawalWorkflow(new CancelWithdrawalWorkflowRequest
        {
            RequestId = id,
            Reason = "Cancelled from Portal",
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> BatchIncrementAsync(BatchIncrementWalletRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.BatchIncrementWallet(request with
        {
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> BatchDecrementAsync(BatchDecrementWalletRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.BatchDecrementWallet(request with
        {
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> BatchTransferAsync(BatchTransferWalletRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.BatchTransferWallet(request with
        {
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> RetryOutboxMessageAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.RetryWalletOutboxMessage(new RetryWalletOutboxMessageRequest
        {
            OutboxMessageId = id,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> ReplayOutboxMessageAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.ReplayWalletOutboxMessage(new ReplayWalletOutboxMessageRequest
        {
            OutboxMessageId = id,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> DeadLetterOutboxMessageAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.DeadLetterWalletOutboxMessage(new DeadLetterWalletOutboxMessageRequest
        {
            OutboxMessageId = id,
            Reason = "Marked dead-letter from Portal",
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> RunReconciliationAsync(Guid? walletId = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.RunWalletReconciliation(new RunWalletReconciliationRequest
        {
            WalletId = walletId,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> MarkReconciliationItemAsync(Guid itemId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.MarkWalletReconciliationItem(new MarkWalletReconciliationItemRequest
        {
            ItemId = itemId,
            Reason = "Marked reconciled from Portal",
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> CreateWalletApprovalAsync(
        WalletOperationType operationType,
        Guid walletId,
        decimal? amount,
        string? reason,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.CreateWalletApproval(new CreateWalletApprovalRequest
        {
            OperationType = operationType,
            WalletId = walletId,
            Amount = amount,
            Reason = reason,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> ApproveWalletApprovalAsync(Guid approvalId, string? reason = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.ApproveWalletApproval(new ApproveWalletApprovalRequest
        {
            ApprovalId = approvalId,
            Reason = reason ?? "Approved from Portal",
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> RejectWalletApprovalAsync(Guid approvalId, string? reason = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.RejectWalletApproval(new RejectWalletApprovalRequest
        {
            ApprovalId = approvalId,
            Reason = reason ?? "Rejected from Portal",
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> SaveWalletPolicyAsync(UpsertWalletPolicyRuleRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.UpsertWalletPolicyRule(request with
        {
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> SaveWalletFeeScheduleAsync(UpsertWalletFeeScheduleRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.UpsertWalletFeeSchedule(request with
        {
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> GenerateStatementAsync(Guid? walletId = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!walletId.HasValue)
        {
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Select a wallet before generating a statement."
            };
        }

        var response = await wallets.WalletStatement(new WalletStatementRequest
        {
            WalletId = walletId.Value,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> CreateWalletCaseAsync(
        Guid walletId,
        WalletCaseType caseType,
        decimal amount,
        Guid? originalTransactionId,
        string? reason,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.CreateWalletCase(new CreateWalletCaseRequest
        {
            WalletId = walletId,
            CaseType = caseType,
            Amount = amount,
            OriginalTransactionId = originalTransactionId,
            Reason = reason,
            ReasonCode = caseType.ToString(),
            ExternalReference = originalTransactionId?.ToString(),
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    public async Task<CmdResponse> ResolveWalletCaseAsync(Guid caseId, bool approve, string? reason, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var response = await wallets.ResolveWalletCase(new ResolveWalletCaseRequest
        {
            CaseId = caseId,
            Approve = approve,
            Reason = reason,
            Metadata = Metadata()
        });
        return ToCommandResponse(response);
    }

    private RequestMetadata Metadata() => new()
    {
        RequestedTenantId = tenantFilter.SelectedTenantId ?? requestMetadata.RequestedTenantId,
        RequestId = Guid.NewGuid(),
        OperationName = requestMetadata.OperationName ?? "Portal",
        DeviceName = requestMetadata.DeviceName,
        UserAgent = requestMetadata.UserAgent,
        IpAddress = requestMetadata.IpAddress
    };

    private static CmdResponse ToCommandResponse(CmdResponse response) => response;

    private static CmdResponse ToCommandResponse<T>(CmdResponse<T> response) =>
        new()
        {
            HttpStatusCode = response.HttpStatusCode,
            Message = response.Message,
            Metadata = response.Metadata
        };
}
