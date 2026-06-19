using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public interface IWalletWorkflowService
{
    Task<Result<WalletWorkflowResponse>> CreateDepositAsync(CreateDepositWorkflowRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> ValidateDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> ApproveDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> RejectDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> SettleDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> FailDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> CancelDepositAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);

    Task<Result<WalletWorkflowResponse>> CreateWithdrawalAsync(CreateWithdrawalWorkflowRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> ValidateWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> ApproveWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> RejectWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> SettleWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> FailWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<WalletWorkflowResponse>> CancelWithdrawalAsync(WalletWorkflowActionRequest request, CancellationToken ct = default);
    Task<Result<int>> ExpireDueAsync(ExpireWalletWorkflowsRequest request, CancellationToken ct = default);
}

public interface IWalletApprovalWorkflowService
{
    Task<Result<WalletApprovalResponse>> CreateAsync(CreateWalletApprovalRequest request, CancellationToken ct = default);
    Task<Result<WalletApprovalResponse>> ApproveAsync(WalletApprovalDecisionRequest request, CancellationToken ct = default);
    Task<Result<WalletApprovalResponse>> RejectAsync(WalletApprovalDecisionRequest request, CancellationToken ct = default);
}

public interface IWalletCaseWorkflowService
{
    Task<Result<WalletCaseResponse>> CreateAsync(CreateWalletCaseRequest request, CancellationToken ct = default);
    Task<Result<WalletCaseResponse>> ResolveAsync(ResolveWalletCaseRequest request, CancellationToken ct = default);
}

public interface IWalletReportingService
{
    Task<Result<List<WalletStatementLineResponse>>> GetStatementAsync(WalletStatementRequest request, CancellationToken ct = default);
    Task<Result<List<WalletStatementLineResponse>>> GetLedgerEntriesAsync(WalletLedgerEntriesRequest request, CancellationToken ct = default);
    Task<Result<WalletBalanceAsOfResponse>> GetBalanceAsOfAsync(WalletBalanceAsOfRequest request, CancellationToken ct = default);
    Task<Result<List<WalletOperationHistoryResponse>>> GetOperationHistoryAsync(WalletOperationHistoryRequest request, CancellationToken ct = default);
    Task<Result<List<WalletOperationHistoryResponse>>> GetFailedRejectedOperationsAsync(WalletFailedRejectedOperationsRequest request, CancellationToken ct = default);
    Task<Result<List<WalletReconciliationItemResponse>>> GetUnreconciledBalancesAsync(WalletUnreconciledBalancesRequest request, CancellationToken ct = default);
    Task<Result<List<WalletOutboxFailureResponse>>> GetOutboxFailuresAsync(WalletOutboxFailuresRequest request, CancellationToken ct = default);
    Task<Result<List<WalletSettlementReportResponse>>> GetSettlementReportAsync(WalletSettlementReportRequest request, CancellationToken ct = default);
}
