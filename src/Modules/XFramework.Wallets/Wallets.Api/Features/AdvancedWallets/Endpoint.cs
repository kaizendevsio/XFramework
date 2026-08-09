using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Wallets.Api.Features.AdvancedWallets;

public static class CreateDepositWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    [MapPost("/api/wallets/deposits", Tags = ["Wallets Deposits"], Summary = "Create deposit request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    public static Task<Result<WalletWorkflowResponse>> Handle(CreateDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.CreateDepositAsync(request, ct);
}

public static class ValidateDepositWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/deposits/validate", Tags = ["Wallets Deposits"], Summary = "Validate deposit request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<WalletWorkflowResponse>> Handle(ValidateDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.ValidateDepositAsync(request, ct);
}

public static class ApproveDepositWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/deposits/approve", Tags = ["Wallets Deposits"], Summary = "Approve deposit request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<WalletWorkflowResponse>> Handle(ApproveDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.ApproveDepositAsync(request, ct);
}

public static class RejectDepositWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/deposits/reject", Tags = ["Wallets Deposits"], Summary = "Reject deposit request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<WalletWorkflowResponse>> Handle(RejectDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.RejectDepositAsync(request, ct);
}

public static class SettleDepositWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/deposits/settle", Tags = ["Wallets Deposits"], Summary = "Settle deposit request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<WalletWorkflowResponse>> Handle(SettleDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.SettleDepositAsync(request, ct);
}

public static class FailDepositWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/deposits/fail", Tags = ["Wallets Deposits"], Summary = "Fail deposit request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<WalletWorkflowResponse>> Handle(FailDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.FailDepositAsync(request, ct);
}

public static class CancelDepositWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    [MapPost("/api/wallets/deposits/cancel", Tags = ["Wallets Deposits"], Summary = "Cancel deposit request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    public static Task<Result<WalletWorkflowResponse>> Handle(CancelDepositWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.CancelDepositAsync(request, ct);
}

public static class CreateWithdrawalWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    [MapPost("/api/wallets/withdrawals", Tags = ["Wallets Withdrawals"], Summary = "Create withdrawal request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    public static Task<Result<WalletWorkflowResponse>> Handle(CreateWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.CreateWithdrawalAsync(request, ct);
}

public static class ValidateWithdrawalWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/withdrawals/validate", Tags = ["Wallets Withdrawals"], Summary = "Validate withdrawal request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<WalletWorkflowResponse>> Handle(ValidateWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.ValidateWithdrawalAsync(request, ct);
}

public static class ApproveWithdrawalWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/withdrawals/approve", Tags = ["Wallets Withdrawals"], Summary = "Approve withdrawal request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<WalletWorkflowResponse>> Handle(ApproveWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.ApproveWithdrawalAsync(request, ct);
}

public static class RejectWithdrawalWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/withdrawals/reject", Tags = ["Wallets Withdrawals"], Summary = "Reject withdrawal request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<WalletWorkflowResponse>> Handle(RejectWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.RejectWithdrawalAsync(request, ct);
}

public static class SettleWithdrawalWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/withdrawals/settle", Tags = ["Wallets Withdrawals"], Summary = "Settle withdrawal request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<WalletWorkflowResponse>> Handle(SettleWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.SettleWithdrawalAsync(request, ct);
}

public static class FailWithdrawalWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/withdrawals/fail", Tags = ["Wallets Withdrawals"], Summary = "Fail withdrawal request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<WalletWorkflowResponse>> Handle(FailWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.FailWithdrawalAsync(request, ct);
}

public static class CancelWithdrawalWorkflowEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    [MapPost("/api/wallets/withdrawals/cancel", Tags = ["Wallets Withdrawals"], Summary = "Cancel withdrawal request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    public static Task<Result<WalletWorkflowResponse>> Handle(CancelWithdrawalWorkflowRequest request, IWalletWorkflowService service, CancellationToken ct) => service.CancelWithdrawalAsync(request, ct);
}

public static class ExpireWalletWorkflowsEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/workflows/expire-due", Tags = ["Wallets Workflows"], Summary = "Expire due wallet workflows", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    public static Task<Result<int>> Handle(ExpireWalletWorkflowsRequest request, IWalletWorkflowService service, CancellationToken ct) => service.ExpireDueAsync(request, ct);
}

public static class IngestWalletPaymentWebhookEndpoint
{
    [MapPost("/api/wallets/payment-webhooks", Tags = ["Wallets Webhooks"], Summary = "Ingest payment webhook", RequireAuthorization = false, ActorRequirement = ActorRequirement.None, TenantAccessMode = TenantAccessMode.Tenantless, AllowAnonymous = true)]
    public static Task<Result<WalletWebhookIngestResponse>> Handle(IngestWalletPaymentWebhookRequest request, IWalletPaymentWebhookService service, CancellationToken ct) => service.IngestAsync(request, ct);
}

public static class IngestWalletPaymentWebhookBoltEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.WalletsAdmin, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    public static Task<Result<WalletWebhookIngestResponse>> Handle(
        IngestWalletPaymentWebhookRequest request,
        IWalletPaymentWebhookService service,
        CancellationToken ct) => service.IngestAsync(request, ct);
}

public static class RetryWalletOutboxEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.WebhooksManage])]
    [MapPost("/api/wallets/outbox/retry", Tags = ["Wallets Outbox"], Summary = "Retry outbox message", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.WebhooksManage])]
    public static Task<Result<WalletOutboxActionResponse>> Handle(RetryWalletOutboxMessageRequest request, IWalletOutboxService service, CancellationToken ct) => service.RetryAsync(request, ct);
}

public static class ReplayWalletOutboxEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.WebhooksManage])]
    [MapPost("/api/wallets/outbox/replay", Tags = ["Wallets Outbox"], Summary = "Replay outbox message", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.WebhooksManage])]
    public static Task<Result<WalletOutboxActionResponse>> Handle(ReplayWalletOutboxMessageRequest request, IWalletOutboxService service, CancellationToken ct) => service.ReplayAsync(request, ct);
}

public static class DeadLetterWalletOutboxEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.WebhooksManage])]
    [MapPost("/api/wallets/outbox/dead-letter", Tags = ["Wallets Outbox"], Summary = "Dead-letter outbox message", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.WebhooksManage])]
    public static Task<Result<WalletOutboxActionResponse>> Handle(DeadLetterWalletOutboxMessageRequest request, IWalletOutboxService service, CancellationToken ct) => service.DeadLetterAsync(request, ct);
}

public static class RunWalletReconciliationEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReconciliationManage])]
    [MapPost("/api/wallets/reconciliation/run", Tags = ["Wallets Reconciliation"], Summary = "Run wallet reconciliation", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReconciliationManage])]
    public static Task<Result<WalletReconciliationRunResponse>> Handle(RunWalletReconciliationRequest request, IWalletReconciliationService service, CancellationToken ct) => service.RunAsync(request, ct);
}

public static class MarkWalletReconciliationEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReconciliationManage])]
    [MapPost("/api/wallets/reconciliation/mark-reconciled", Tags = ["Wallets Reconciliation"], Summary = "Mark reconciliation item reconciled", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReconciliationManage])]
    public static Task<Result<WalletReconciliationItemResponse>> Handle(MarkWalletReconciliationItemRequest request, IWalletReconciliationService service, CancellationToken ct) => service.MarkReconciledAsync(request, ct);
}

public static class ApproveWalletApprovalEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.PolicyManage])]
    [MapPost("/api/wallets/approvals/approve", Tags = ["Wallets Approvals"], Summary = "Approve maker-checker request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.PolicyManage])]
    public static Task<Result<WalletApprovalResponse>> Handle(ApproveWalletApprovalRequest request, IWalletApprovalWorkflowService service, CancellationToken ct) => service.ApproveAsync(request, ct);
}

public static class CreateWalletApprovalEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    [MapPost("/api/wallets/approvals", Tags = ["Wallets Approvals"], Summary = "Create maker-checker request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    public static Task<Result<WalletApprovalResponse>> Handle(CreateWalletApprovalRequest request, IWalletApprovalWorkflowService service, CancellationToken ct) => service.CreateAsync(request, ct);
}

public static class RejectWalletApprovalEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.PolicyManage])]
    [MapPost("/api/wallets/approvals/reject", Tags = ["Wallets Approvals"], Summary = "Reject maker-checker request", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.PolicyManage])]
    public static Task<Result<WalletApprovalResponse>> Handle(RejectWalletApprovalRequest request, IWalletApprovalWorkflowService service, CancellationToken ct) => service.RejectAsync(request, ct);
}

public static class CreateWalletCaseEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    [MapPost("/api/wallets/cases", Tags = ["Wallets Cases"], Summary = "Create refund, dispute, or chargeback case", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    public static Task<Result<WalletCaseResponse>> Handle(CreateWalletCaseRequest request, IWalletCaseWorkflowService service, CancellationToken ct) => service.CreateAsync(request, ct);
}

public static class ResolveWalletCaseEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.PolicyManage])]
    [MapPost("/api/wallets/cases/resolve", Tags = ["Wallets Cases"], Summary = "Resolve refund, dispute, or chargeback case", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.PolicyManage])]
    public static Task<Result<WalletCaseResponse>> Handle(ResolveWalletCaseRequest request, IWalletCaseWorkflowService service, CancellationToken ct) => service.ResolveAsync(request, ct);
}

public static class UpsertWalletPolicyRuleEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.PolicyManage])]
    [MapPost("/api/wallets/policy/rules/upsert", Tags = ["Wallets Policy"], Summary = "Create or update wallet policy rule", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.PolicyManage])]
    public static Task<Result<WalletPolicyRuleResponse>> Handle(UpsertWalletPolicyRuleRequest request, IWalletPolicyAdminService service, CancellationToken ct) => service.UpsertPolicyRuleAsync(request, ct);
}

public static class UpsertWalletFeeScheduleEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.PolicyManage])]
    [MapPost("/api/wallets/policy/fee-schedules/upsert", Tags = ["Wallets Policy"], Summary = "Create or update wallet fee schedule", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.PolicyManage])]
    public static Task<Result<WalletFeeScheduleResponse>> Handle(UpsertWalletFeeScheduleRequest request, IWalletPolicyAdminService service, CancellationToken ct) => service.UpsertFeeScheduleAsync(request, ct);
}

public static class WalletStatementEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    [MapPost("/api/wallets/reports/statement", Tags = ["Wallets Reporting"], Summary = "Get wallet statement", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    public static Task<Result<List<WalletStatementLineResponse>>> Handle(WalletStatementRequest request, IWalletReportingService service, CancellationToken ct) => service.GetStatementAsync(request, ct);
}

public static class WalletLedgerEntriesReportEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    [MapPost("/api/wallets/reports/ledger-entries", Tags = ["Wallets Reporting"], Summary = "Get wallet ledger entries", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    public static Task<Result<List<WalletStatementLineResponse>>> Handle(WalletLedgerEntriesRequest request, IWalletReportingService service, CancellationToken ct) => service.GetLedgerEntriesAsync(request, ct);
}

public static class WalletBalanceAsOfEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    [MapPost("/api/wallets/reports/balance-as-of", Tags = ["Wallets Reporting"], Summary = "Get wallet balance as of a date", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    public static Task<Result<WalletBalanceAsOfResponse>> Handle(WalletBalanceAsOfRequest request, IWalletReportingService service, CancellationToken ct) => service.GetBalanceAsOfAsync(request, ct);
}

public static class WalletOperationHistoryEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    [MapPost("/api/wallets/reports/operation-history", Tags = ["Wallets Reporting"], Summary = "Get wallet operation history", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    public static Task<Result<List<WalletOperationHistoryResponse>>> Handle(WalletOperationHistoryRequest request, IWalletReportingService service, CancellationToken ct) => service.GetOperationHistoryAsync(request, ct);
}

public static class WalletFailedRejectedOperationsEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    [MapPost("/api/wallets/reports/failed-rejected-operations", Tags = ["Wallets Reporting"], Summary = "Get failed and rejected wallet operations", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    public static Task<Result<List<WalletOperationHistoryResponse>>> Handle(WalletFailedRejectedOperationsRequest request, IWalletReportingService service, CancellationToken ct) => service.GetFailedRejectedOperationsAsync(request, ct);
}

public static class WalletUnreconciledBalancesEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    [MapPost("/api/wallets/reports/unreconciled-balances", Tags = ["Wallets Reporting"], Summary = "Get unreconciled wallet balances", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    public static Task<Result<List<WalletReconciliationItemResponse>>> Handle(WalletUnreconciledBalancesRequest request, IWalletReportingService service, CancellationToken ct) => service.GetUnreconciledBalancesAsync(request, ct);
}

public static class WalletOutboxFailuresReportEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    [MapPost("/api/wallets/reports/outbox-failures", Tags = ["Wallets Reporting"], Summary = "Get wallet outbox failures", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    public static Task<Result<List<WalletOutboxFailureResponse>>> Handle(WalletOutboxFailuresRequest request, IWalletReportingService service, CancellationToken ct) => service.GetOutboxFailuresAsync(request, ct);
}

public static class WalletSettlementReportEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    [MapPost("/api/wallets/reports/settlements", Tags = ["Wallets Reporting"], Summary = "Get deposit and withdrawal settlement report", RequireAuthorization = true, RequiredActorCapabilities = [WalletAuthorizationCapabilities.ReportingView])]
    public static Task<Result<List<WalletSettlementReportResponse>>> Handle(WalletSettlementReportRequest request, IWalletReportingService service, CancellationToken ct) => service.GetSettlementReportAsync(request, ct);
}
