using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.AdvancedWallets;

public static class CreateDepositWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/deposits", Tags = ["Wallets Deposits"], Summary = "Create deposit request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(CreateDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.CreateDepositAsync(request, ct);
}

public static class ValidateDepositWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/deposits/validate", Tags = ["Wallets Deposits"], Summary = "Validate deposit request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(ValidateDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.ValidateDepositAsync(request, ct);
}

public static class ApproveDepositWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/deposits/approve", Tags = ["Wallets Deposits"], Summary = "Approve deposit request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(ApproveDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.ApproveDepositAsync(request, ct);
}

public static class RejectDepositWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/deposits/reject", Tags = ["Wallets Deposits"], Summary = "Reject deposit request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(RejectDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.RejectDepositAsync(request, ct);
}

public static class SettleDepositWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/deposits/settle", Tags = ["Wallets Deposits"], Summary = "Settle deposit request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(SettleDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.SettleDepositAsync(request, ct);
}

public static class FailDepositWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/deposits/fail", Tags = ["Wallets Deposits"], Summary = "Fail deposit request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(FailDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.FailDepositAsync(request, ct);
}

public static class CancelDepositWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/deposits/cancel", Tags = ["Wallets Deposits"], Summary = "Cancel deposit request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(CancelDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.CancelDepositAsync(request, ct);
}

public static class CreateWithdrawalWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/withdrawals", Tags = ["Wallets Withdrawals"], Summary = "Create withdrawal request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(CreateWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.CreateWithdrawalAsync(request, ct);
}

public static class ValidateWithdrawalWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/withdrawals/validate", Tags = ["Wallets Withdrawals"], Summary = "Validate withdrawal request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(ValidateWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.ValidateWithdrawalAsync(request, ct);
}

public static class ApproveWithdrawalWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/withdrawals/approve", Tags = ["Wallets Withdrawals"], Summary = "Approve withdrawal request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(ApproveWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.ApproveWithdrawalAsync(request, ct);
}

public static class RejectWithdrawalWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/withdrawals/reject", Tags = ["Wallets Withdrawals"], Summary = "Reject withdrawal request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(RejectWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.RejectWithdrawalAsync(request, ct);
}

public static class SettleWithdrawalWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/withdrawals/settle", Tags = ["Wallets Withdrawals"], Summary = "Settle withdrawal request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(SettleWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.SettleWithdrawalAsync(request, ct);
}

public static class FailWithdrawalWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/withdrawals/fail", Tags = ["Wallets Withdrawals"], Summary = "Fail withdrawal request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(FailWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.FailWithdrawalAsync(request, ct);
}

public static class CancelWithdrawalWorkflowEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/withdrawals/cancel", Tags = ["Wallets Withdrawals"], Summary = "Cancel withdrawal request", RequireAuthorization = true)]
    public static Task<Result<WalletWorkflowResponse>> Handle(CancelWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.CancelWithdrawalAsync(request, ct);
}

public static class ExpireWalletWorkflowsEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/workflows/expire-due", Tags = ["Wallets Workflows"], Summary = "Expire due wallet workflows", RequireAuthorization = true)]
    public static Task<Result<int>> Handle(ExpireWalletWorkflowsRequest request, IWalletWorkflowService service, CancellationToken ct) => service.ExpireDueAsync(request, ct);
}

public static class IngestWalletPaymentWebhookEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/payment-webhooks", Tags = ["Wallets Webhooks"], Summary = "Ingest payment webhook")]
    public static Task<Result<WalletWebhookIngestResponse>> Handle(IngestWalletPaymentWebhookRequest request, IWalletPaymentWebhookService service, CancellationToken ct) => service.IngestAsync(request, ct);
}

public static class RetryWalletOutboxEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/outbox/retry", Tags = ["Wallets Outbox"], Summary = "Retry outbox message", RequireAuthorization = true)]
    public static Task<Result<WalletOutboxActionResponse>> Handle(RetryWalletOutboxMessageRequest request, IWalletOutboxService service, CancellationToken ct) => service.RetryAsync(request, ct);
}

public static class ReplayWalletOutboxEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/outbox/replay", Tags = ["Wallets Outbox"], Summary = "Replay outbox message", RequireAuthorization = true)]
    public static Task<Result<WalletOutboxActionResponse>> Handle(ReplayWalletOutboxMessageRequest request, IWalletOutboxService service, CancellationToken ct) => service.ReplayAsync(request, ct);
}

public static class DeadLetterWalletOutboxEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/outbox/dead-letter", Tags = ["Wallets Outbox"], Summary = "Dead-letter outbox message", RequireAuthorization = true)]
    public static Task<Result<WalletOutboxActionResponse>> Handle(DeadLetterWalletOutboxMessageRequest request, IWalletOutboxService service, CancellationToken ct) => service.DeadLetterAsync(request, ct);
}

public static class RunWalletReconciliationEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reconciliation/run", Tags = ["Wallets Reconciliation"], Summary = "Run wallet reconciliation", RequireAuthorization = true)]
    public static Task<Result<WalletReconciliationRunResponse>> Handle(RunWalletReconciliationRequest request, IWalletReconciliationService service, CancellationToken ct) => service.RunAsync(request, ct);
}

public static class MarkWalletReconciliationEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reconciliation/mark-reconciled", Tags = ["Wallets Reconciliation"], Summary = "Mark reconciliation item reconciled", RequireAuthorization = true)]
    public static Task<Result<WalletReconciliationItemResponse>> Handle(MarkWalletReconciliationItemRequest request, IWalletReconciliationService service, CancellationToken ct) => service.MarkReconciledAsync(request, ct);
}

public static class ApproveWalletApprovalEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/approvals/approve", Tags = ["Wallets Approvals"], Summary = "Approve maker-checker request", RequireAuthorization = true)]
    public static Task<Result<WalletApprovalResponse>> Handle(ApproveWalletApprovalRequest request, IWalletApprovalWorkflowService service, CancellationToken ct) => service.ApproveAsync(request, ct);
}

public static class CreateWalletApprovalEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/approvals", Tags = ["Wallets Approvals"], Summary = "Create maker-checker request", RequireAuthorization = true)]
    public static Task<Result<WalletApprovalResponse>> Handle(CreateWalletApprovalRequest request, IWalletApprovalWorkflowService service, CancellationToken ct) => service.CreateAsync(request, ct);
}

public static class RejectWalletApprovalEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/approvals/reject", Tags = ["Wallets Approvals"], Summary = "Reject maker-checker request", RequireAuthorization = true)]
    public static Task<Result<WalletApprovalResponse>> Handle(RejectWalletApprovalRequest request, IWalletApprovalWorkflowService service, CancellationToken ct) => service.RejectAsync(request, ct);
}

public static class CreateWalletCaseEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/cases", Tags = ["Wallets Cases"], Summary = "Create refund, dispute, or chargeback case", RequireAuthorization = true)]
    public static Task<Result<WalletCaseResponse>> Handle(CreateWalletCaseRequest request, IWalletCaseWorkflowService service, CancellationToken ct) => service.CreateAsync(request, ct);
}

public static class ResolveWalletCaseEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/cases/resolve", Tags = ["Wallets Cases"], Summary = "Resolve refund, dispute, or chargeback case", RequireAuthorization = true)]
    public static Task<Result<WalletCaseResponse>> Handle(ResolveWalletCaseRequest request, IWalletCaseWorkflowService service, CancellationToken ct) => service.ResolveAsync(request, ct);
}

public static class UpsertWalletPolicyRuleEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/policy/rules/upsert", Tags = ["Wallets Policy"], Summary = "Create or update wallet policy rule", RequireAuthorization = true)]
    public static Task<Result<WalletPolicyRuleResponse>> Handle(UpsertWalletPolicyRuleRequest request, IWalletPolicyAdminService service, CancellationToken ct) => service.UpsertPolicyRuleAsync(request, ct);
}

public static class UpsertWalletFeeScheduleEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/policy/fee-schedules/upsert", Tags = ["Wallets Policy"], Summary = "Create or update wallet fee schedule", RequireAuthorization = true)]
    public static Task<Result<WalletFeeScheduleResponse>> Handle(UpsertWalletFeeScheduleRequest request, IWalletPolicyAdminService service, CancellationToken ct) => service.UpsertFeeScheduleAsync(request, ct);
}

public static class WalletStatementEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reports/statement", Tags = ["Wallets Reporting"], Summary = "Get wallet statement", RequireAuthorization = true)]
    public static Task<Result<List<WalletStatementLineResponse>>> Handle(WalletStatementRequest request, IWalletReportingService service, CancellationToken ct) => service.GetStatementAsync(request, ct);
}

public static class WalletLedgerEntriesReportEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reports/ledger-entries", Tags = ["Wallets Reporting"], Summary = "Get wallet ledger entries", RequireAuthorization = true)]
    public static Task<Result<List<WalletStatementLineResponse>>> Handle(WalletLedgerEntriesRequest request, IWalletReportingService service, CancellationToken ct) => service.GetLedgerEntriesAsync(request, ct);
}

public static class WalletBalanceAsOfEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reports/balance-as-of", Tags = ["Wallets Reporting"], Summary = "Get wallet balance as of a date", RequireAuthorization = true)]
    public static Task<Result<WalletBalanceAsOfResponse>> Handle(WalletBalanceAsOfRequest request, IWalletReportingService service, CancellationToken ct) => service.GetBalanceAsOfAsync(request, ct);
}

public static class WalletOperationHistoryEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reports/operation-history", Tags = ["Wallets Reporting"], Summary = "Get wallet operation history", RequireAuthorization = true)]
    public static Task<Result<List<WalletOperationHistoryResponse>>> Handle(WalletOperationHistoryRequest request, IWalletReportingService service, CancellationToken ct) => service.GetOperationHistoryAsync(request, ct);
}

public static class WalletFailedRejectedOperationsEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reports/failed-rejected-operations", Tags = ["Wallets Reporting"], Summary = "Get failed and rejected wallet operations", RequireAuthorization = true)]
    public static Task<Result<List<WalletOperationHistoryResponse>>> Handle(WalletFailedRejectedOperationsRequest request, IWalletReportingService service, CancellationToken ct) => service.GetFailedRejectedOperationsAsync(request, ct);
}

public static class WalletUnreconciledBalancesEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reports/unreconciled-balances", Tags = ["Wallets Reporting"], Summary = "Get unreconciled wallet balances", RequireAuthorization = true)]
    public static Task<Result<List<WalletReconciliationItemResponse>>> Handle(WalletUnreconciledBalancesRequest request, IWalletReportingService service, CancellationToken ct) => service.GetUnreconciledBalancesAsync(request, ct);
}

public static class WalletOutboxFailuresReportEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reports/outbox-failures", Tags = ["Wallets Reporting"], Summary = "Get wallet outbox failures", RequireAuthorization = true)]
    public static Task<Result<List<WalletOutboxFailureResponse>>> Handle(WalletOutboxFailuresRequest request, IWalletReportingService service, CancellationToken ct) => service.GetOutboxFailuresAsync(request, ct);
}

public static class WalletSettlementReportEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reports/settlements", Tags = ["Wallets Reporting"], Summary = "Get deposit and withdrawal settlement report", RequireAuthorization = true)]
    public static Task<Result<List<WalletSettlementReportResponse>>> Handle(WalletSettlementReportRequest request, IWalletReportingService service, CancellationToken ct) => service.GetSettlementReportAsync(request, ct);
}
