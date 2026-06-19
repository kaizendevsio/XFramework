using Wallets.Domain.Shared.Contracts.Responses;

namespace Wallets.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CreateDepositWorkflowRequest : RequestBase,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<CreateDepositWorkflowRequest, CmdResponse<WalletWorkflowResponse>>
{
    public Guid CredentialId { get; init; }
    public Guid? WalletId { get; init; }
    public Guid? WalletTypeId { get; init; }
    public Guid? CurrencyId { get; init; }
    public Guid GatewayId { get; init; }
    public decimal Amount { get; init; }
    public decimal? RequestedFee { get; init; }
    public string? Address { get; init; }
    public string? Remarks { get; init; }
    public string? ExternalReference { get; init; }
    public string? IdempotencyKey { get; init; }
    public DateTime? ExpiryDate { get; init; }
}

[MemoryPackable]
public partial record CreateWithdrawalWorkflowRequest : RequestBase,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<CreateWithdrawalWorkflowRequest, CmdResponse<WalletWorkflowResponse>>
{
    public Guid CredentialId { get; init; }
    public Guid WalletId { get; init; }
    public Guid? GatewayId { get; init; }
    public Guid? CurrencyId { get; init; }
    public decimal Amount { get; init; }
    public decimal? RequestedFee { get; init; }
    public string? Address { get; init; }
    public string? Remarks { get; init; }
    public string? ExternalReference { get; init; }
    public string? IdempotencyKey { get; init; }
}

[MemoryPackable]
public partial record WalletWorkflowActionRequest : RequestBase
{
    public Guid RequestId { get; init; }
    public string? Reason { get; init; }
    public string? ProviderEventId { get; init; }
    public string? ProviderTransactionId { get; init; }
    public string? ProviderStatus { get; init; }
    public string? ExternalReference { get; init; }
    public string? RawProviderPayloadJson { get; init; }
    public string? IdempotencyKey { get; init; }
    public Guid? WebhookEventId { get; init; }
}

[MemoryPackable]
public partial record ValidateDepositWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<ValidateDepositWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record ApproveDepositWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<ApproveDepositWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record RejectDepositWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<RejectDepositWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record SettleDepositWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<SettleDepositWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record FailDepositWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<FailDepositWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record CancelDepositWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<CancelDepositWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record ValidateWithdrawalWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<ValidateWithdrawalWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record ApproveWithdrawalWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<ApproveWithdrawalWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record RejectWithdrawalWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<RejectWithdrawalWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record SettleWithdrawalWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<SettleWithdrawalWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record FailWithdrawalWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<FailWithdrawalWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record CancelWithdrawalWorkflowRequest : WalletWorkflowActionRequest,
    ICommand<CmdResponse<WalletWorkflowResponse>>,
    IBoltRequest<CancelWithdrawalWorkflowRequest, CmdResponse<WalletWorkflowResponse>>;

[MemoryPackable]
public partial record ExpireWalletWorkflowsRequest : RequestBase,
    ICommand<CmdResponse<int>>,
    IBoltRequest<ExpireWalletWorkflowsRequest, CmdResponse<int>>
{
    public bool IncludeDeposits { get; init; } = true;
    public bool IncludeWithdrawals { get; init; } = true;
}

[MemoryPackable]
public partial record IngestWalletPaymentWebhookRequest : RequestBase,
    ICommand<CmdResponse<WalletWebhookIngestResponse>>,
    IBoltRequest<IngestWalletPaymentWebhookRequest, CmdResponse<WalletWebhookIngestResponse>>
{
    public string ProviderKey { get; init; } = string.Empty;
    public string ExternalEventId { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public string? ProviderTransactionId { get; init; }
    public string? ProviderStatus { get; init; }
    public decimal? Amount { get; init; }
    public string RawPayloadJson { get; init; } = "{}";
    public Dictionary<string, string> Headers { get; init; } = [];
    public string? Signature { get; init; }
}

[MemoryPackable]
public partial record WalletOutboxActionRequest : RequestBase
{
    public Guid OutboxMessageId { get; init; }
    public string? Reason { get; init; }
}

[MemoryPackable]
public partial record RetryWalletOutboxMessageRequest : WalletOutboxActionRequest,
    ICommand<CmdResponse<WalletOutboxActionResponse>>,
    IBoltRequest<RetryWalletOutboxMessageRequest, CmdResponse<WalletOutboxActionResponse>>;

[MemoryPackable]
public partial record ReplayWalletOutboxMessageRequest : WalletOutboxActionRequest,
    ICommand<CmdResponse<WalletOutboxActionResponse>>,
    IBoltRequest<ReplayWalletOutboxMessageRequest, CmdResponse<WalletOutboxActionResponse>>;

[MemoryPackable]
public partial record DeadLetterWalletOutboxMessageRequest : WalletOutboxActionRequest,
    ICommand<CmdResponse<WalletOutboxActionResponse>>,
    IBoltRequest<DeadLetterWalletOutboxMessageRequest, CmdResponse<WalletOutboxActionResponse>>;

[MemoryPackable]
public partial record RunWalletReconciliationRequest : RequestBase,
    ICommand<CmdResponse<WalletReconciliationRunResponse>>,
    IBoltRequest<RunWalletReconciliationRequest, CmdResponse<WalletReconciliationRunResponse>>
{
    public Guid? WalletId { get; init; }
}

[MemoryPackable]
public partial record MarkWalletReconciliationItemRequest : RequestBase,
    ICommand<CmdResponse<WalletReconciliationItemResponse>>,
    IBoltRequest<MarkWalletReconciliationItemRequest, CmdResponse<WalletReconciliationItemResponse>>
{
    public Guid ItemId { get; init; }
    public string? Reason { get; init; }
}

[MemoryPackable]
public partial record CreateWalletApprovalRequest : RequestBase,
    ICommand<CmdResponse<WalletApprovalResponse>>,
    IBoltRequest<CreateWalletApprovalRequest, CmdResponse<WalletApprovalResponse>>
{
    public WalletOperationType OperationType { get; init; }
    public Guid? WalletId { get; init; }
    public decimal? Amount { get; init; }
    public string? Reason { get; init; }
    public string? AuditMetadataJson { get; init; }
}

[MemoryPackable]
public partial record WalletApprovalDecisionRequest : RequestBase
{
    public Guid ApprovalId { get; init; }
    public string? Reason { get; init; }
}

[MemoryPackable]
public partial record ApproveWalletApprovalRequest : WalletApprovalDecisionRequest,
    ICommand<CmdResponse<WalletApprovalResponse>>,
    IBoltRequest<ApproveWalletApprovalRequest, CmdResponse<WalletApprovalResponse>>;

[MemoryPackable]
public partial record RejectWalletApprovalRequest : WalletApprovalDecisionRequest,
    ICommand<CmdResponse<WalletApprovalResponse>>,
    IBoltRequest<RejectWalletApprovalRequest, CmdResponse<WalletApprovalResponse>>;

[MemoryPackable]
public partial record CreateWalletCaseRequest : RequestBase,
    ICommand<CmdResponse<WalletCaseResponse>>,
    IBoltRequest<CreateWalletCaseRequest, CmdResponse<WalletCaseResponse>>
{
    public WalletCaseType CaseType { get; init; }
    public Guid WalletId { get; init; }
    public Guid? OriginalOperationId { get; init; }
    public Guid? OriginalTransactionId { get; init; }
    public decimal Amount { get; init; }
    public string? ExternalReference { get; init; }
    public string? ReasonCode { get; init; }
    public string? Reason { get; init; }
    public string? IdempotencyKey { get; init; }
}

[MemoryPackable]
public partial record ResolveWalletCaseRequest : RequestBase,
    ICommand<CmdResponse<WalletCaseResponse>>,
    IBoltRequest<ResolveWalletCaseRequest, CmdResponse<WalletCaseResponse>>
{
    public Guid CaseId { get; init; }
    public bool Approve { get; init; }
    public string? Reason { get; init; }
    public string? IdempotencyKey { get; init; }
}

[MemoryPackable]
public partial record UpsertWalletPolicyRuleRequest : RequestBase,
    ICommand<CmdResponse<WalletPolicyRuleResponse>>,
    IBoltRequest<UpsertWalletPolicyRuleRequest, CmdResponse<WalletPolicyRuleResponse>>
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public WalletOperationType? OperationType { get; init; }
    public Guid? WalletTypeId { get; init; }
    public Guid? CurrencyId { get; init; }
    public WalletStatus? RequiredWalletStatus { get; init; }
    public decimal? MaxSingleTransactionAmount { get; init; }
    public decimal? DailyVelocityLimit { get; init; }
    public decimal? MonthlyVelocityLimit { get; init; }
    public decimal? ApprovalThreshold { get; init; }
    public bool DenyWhenMatched { get; init; }
    public string? RiskTier { get; init; }
    public string? DecisionCode { get; init; }
    public DateTime? EffectiveAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsEnabled { get; init; } = true;
}

[MemoryPackable]
public partial record UpsertWalletFeeScheduleRequest : RequestBase,
    ICommand<CmdResponse<WalletFeeScheduleResponse>>,
    IBoltRequest<UpsertWalletFeeScheduleRequest, CmdResponse<WalletFeeScheduleResponse>>
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public WalletOperationType OperationType { get; init; }
    public Guid? WalletTypeId { get; init; }
    public Guid? CurrencyId { get; init; }
    public decimal FixedFee { get; init; }
    public decimal PercentageFee { get; init; }
    public decimal? MinimumFee { get; init; }
    public decimal? MaximumFee { get; init; }
    public bool AllowRequestedFeeOverride { get; init; }
    public DateTime? EffectiveAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsEnabled { get; init; } = true;
}

[MemoryPackable]
public partial record WalletStatementRequest : RequestBase,
    ICommand<CmdResponse<List<WalletStatementLineResponse>>>,
    IBoltRequest<WalletStatementRequest, CmdResponse<List<WalletStatementLineResponse>>>
{
    public Guid WalletId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? ReferenceNumber { get; init; }
}

[MemoryPackable]
public partial record WalletLedgerEntriesRequest : RequestBase,
    ICommand<CmdResponse<List<WalletStatementLineResponse>>>,
    IBoltRequest<WalletLedgerEntriesRequest, CmdResponse<List<WalletStatementLineResponse>>>
{
    public Guid? WalletId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? ReferenceNumber { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

[MemoryPackable]
public partial record WalletBalanceAsOfRequest : RequestBase,
    ICommand<CmdResponse<WalletBalanceAsOfResponse>>,
    IBoltRequest<WalletBalanceAsOfRequest, CmdResponse<WalletBalanceAsOfResponse>>
{
    public Guid WalletId { get; init; }
    public DateTime AsOf { get; init; }
}

[MemoryPackable]
public partial record WalletOperationHistoryRequest : RequestBase,
    ICommand<CmdResponse<List<WalletOperationHistoryResponse>>>,
    IBoltRequest<WalletOperationHistoryRequest, CmdResponse<List<WalletOperationHistoryResponse>>>
{
    public Guid? WalletId { get; init; }
    public Guid? ActorCredentialId { get; init; }
    public WalletOperationType? OperationType { get; init; }
    public WalletOperationStatus? Status { get; init; }
    public string? ReferenceNumber { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

[MemoryPackable]
public partial record WalletFailedRejectedOperationsRequest : RequestBase,
    ICommand<CmdResponse<List<WalletOperationHistoryResponse>>>,
    IBoltRequest<WalletFailedRejectedOperationsRequest, CmdResponse<List<WalletOperationHistoryResponse>>>
{
    public Guid? WalletId { get; init; }
    public WalletOperationType? OperationType { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

[MemoryPackable]
public partial record WalletUnreconciledBalancesRequest : RequestBase,
    ICommand<CmdResponse<List<WalletReconciliationItemResponse>>>,
    IBoltRequest<WalletUnreconciledBalancesRequest, CmdResponse<List<WalletReconciliationItemResponse>>>
{
    public Guid? WalletId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

[MemoryPackable]
public partial record WalletOutboxFailuresRequest : RequestBase,
    ICommand<CmdResponse<List<WalletOutboxFailureResponse>>>,
    IBoltRequest<WalletOutboxFailuresRequest, CmdResponse<List<WalletOutboxFailureResponse>>>
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

[MemoryPackable]
public partial record WalletSettlementReportRequest : RequestBase,
    ICommand<CmdResponse<List<WalletSettlementReportResponse>>>,
    IBoltRequest<WalletSettlementReportRequest, CmdResponse<List<WalletSettlementReportResponse>>>
{
    public bool IncludeDeposits { get; init; } = true;
    public bool IncludeWithdrawals { get; init; } = true;
    public WalletWorkflowStatus? Status { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}
