namespace Wallets.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record WalletWorkflowResponse
{
    public Guid Id { get; init; }
    public Guid? WalletId { get; init; }
    public WalletOperationType OperationType { get; init; }
    public WalletWorkflowStatus Status { get; init; }
    public decimal Amount { get; init; }
    public decimal RequestedFee { get; init; }
    public decimal CalculatedFee { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? ExternalReference { get; init; }
    public Guid? ApprovalId { get; init; }
    public Guid? OperationId { get; init; }
    public Guid? TransactionId { get; init; }
    public string? Message { get; init; }
}

[MemoryPackable]
public partial record WalletWebhookIngestResponse
{
    public Guid WebhookEventId { get; init; }
    public WalletWebhookProcessingStatus Status { get; init; }
    public bool Duplicate { get; init; }
    public Guid? DepositRequestId { get; init; }
    public Guid? WithdrawalRequestId { get; init; }
    public Guid? OperationId { get; init; }
    public string? Message { get; init; }
}

[MemoryPackable]
public partial record WalletOutboxActionResponse
{
    public Guid OutboxMessageId { get; init; }
    public WalletOutboxStatus Status { get; init; }
    public int Attempts { get; init; }
    public DateTime? NextAttemptAt { get; init; }
    public string? Message { get; init; }
}

[MemoryPackable]
public partial record WalletApprovalResponse
{
    public Guid ApprovalId { get; init; }
    public WalletApprovalStatus Status { get; init; }
    public Guid? OperationId { get; init; }
    public string? Message { get; init; }
}

[MemoryPackable]
public partial record WalletReconciliationRunResponse
{
    public Guid RunId { get; init; }
    public WalletReconciliationStatus Status { get; init; }
    public int CheckedCount { get; init; }
    public int DriftCount { get; init; }
    public string? Message { get; init; }
}

[MemoryPackable]
public partial record WalletReconciliationItemResponse
{
    public Guid Id { get; init; }
    public Guid? WalletId { get; init; }
    public string CheckType { get; init; } = string.Empty;
    public WalletReconciliationStatus Status { get; init; }
    public decimal ExpectedAmount { get; init; }
    public decimal ActualAmount { get; init; }
    public decimal DriftAmount { get; init; }
    public string? RepairSuggestion { get; init; }
}

[MemoryPackable]
public partial record WalletPolicyDecisionResponse
{
    public bool IsApproved { get; init; }
    public bool RequiresApproval { get; init; }
    public string Decision { get; init; } = string.Empty;
    public string? Message { get; init; }
    public decimal? RequestedAmount { get; init; }
    public decimal? DailyVelocity { get; init; }
    public decimal? MonthlyVelocity { get; init; }
}

[MemoryPackable]
public partial record WalletFeeQuoteResponse
{
    public decimal RequestedFee { get; init; }
    public decimal CalculatedFee { get; init; }
    public bool OverrideAllowed { get; init; }
    public Guid? FeeScheduleId { get; init; }
}

[MemoryPackable]
public partial record WalletCaseResponse
{
    public Guid CaseId { get; init; }
    public WalletCaseType CaseType { get; init; }
    public WalletCaseStatus Status { get; init; }
    public Guid WalletId { get; init; }
    public Guid? OperationId { get; init; }
    public decimal Amount { get; init; }
    public string? Message { get; init; }
}

[MemoryPackable]
public partial record WalletPolicyRuleResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public WalletOperationType? OperationType { get; init; }
    public Guid? WalletTypeId { get; init; }
    public decimal? MaxSingleTransactionAmount { get; init; }
    public decimal? DailyVelocityLimit { get; init; }
    public decimal? MonthlyVelocityLimit { get; init; }
    public decimal? ApprovalThreshold { get; init; }
    public bool DenyWhenMatched { get; init; }
    public bool IsEnabled { get; init; }
    public string? Message { get; init; }
}

[MemoryPackable]
public partial record WalletFeeScheduleResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public WalletOperationType OperationType { get; init; }
    public Guid? WalletTypeId { get; init; }
    public decimal FixedFee { get; init; }
    public decimal PercentageFee { get; init; }
    public decimal? MinimumFee { get; init; }
    public decimal? MaximumFee { get; init; }
    public bool AllowRequestedFeeOverride { get; init; }
    public bool IsEnabled { get; init; }
    public string? Message { get; init; }
}

[MemoryPackable]
public partial record WalletStatementLineResponse
{
    public DateTime CreatedAt { get; init; }
    public Guid OperationId { get; init; }
    public Guid? LedgerEntryId { get; init; }
    public Guid? TransactionId { get; init; }
    public WalletOperationType OperationType { get; init; }
    public WalletLedgerDirection Direction { get; init; }
    public WalletBalanceBucket BalanceBucket { get; init; }
    public decimal Amount { get; init; }
    public decimal? RunningBalance { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? Description { get; init; }
}

[MemoryPackable]
public partial record WalletBalanceAsOfResponse
{
    public Guid WalletId { get; init; }
    public DateTime AsOf { get; init; }
    public decimal Balance { get; init; }
    public decimal AvailableBalance { get; init; }
    public decimal DebitOnHoldBalance { get; init; }
    public decimal CreditOnHoldBalance { get; init; }
}

[MemoryPackable]
public partial record WalletOperationHistoryResponse
{
    public Guid OperationId { get; init; }
    public WalletOperationType OperationType { get; init; }
    public WalletOperationStatus Status { get; init; }
    public Guid? WalletId { get; init; }
    public Guid? ActorCredentialId { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? ExternalReference { get; init; }
    public decimal? RequestedFee { get; init; }
    public decimal? CalculatedFee { get; init; }
    public bool RequiresApproval { get; init; }
    public string? FailureMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

[MemoryPackable]
public partial record WalletOutboxFailureResponse
{
    public Guid OutboxMessageId { get; init; }
    public Guid AggregateId { get; init; }
    public string AggregateType { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public WalletOutboxStatus Status { get; init; }
    public int Attempts { get; init; }
    public DateTime? NextAttemptAt { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? LastError { get; init; }
    public DateTime CreatedAt { get; init; }
}

[MemoryPackable]
public partial record WalletSettlementReportResponse
{
    public string WorkflowType { get; init; } = string.Empty;
    public Guid RequestId { get; init; }
    public Guid? WalletId { get; init; }
    public Guid CredentialId { get; init; }
    public WalletWorkflowStatus Status { get; init; }
    public decimal Amount { get; init; }
    public decimal RequestedFee { get; init; }
    public decimal CalculatedFee { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? ExternalReference { get; init; }
    public string? ProviderStatus { get; init; }
    public Guid? SettlementOperationId { get; init; }
    public Guid? SettlementTransactionId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SettledAt { get; init; }
}
