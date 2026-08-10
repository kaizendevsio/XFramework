using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/withdrawal-requests",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets.reporting",
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "withdrawal-requests"
)]
public partial class WithdrawalRequest : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public string? Address { get; set; }

    [MemoryPackOrder(2)]
    public decimal? Amount { get; set; }
    
    [MemoryPackOrder(3)]
    public decimal? Fee { get; set; }

    [MemoryPackOrder(4)]
    public TransactionStatus WithdrawalStatus { get; set; }

    [MemoryPackOrder(5)]
    public string? Remarks { get; set; }
    
    [MemoryPackOrder(6)]
    public string? ReferenceNumber { get; set; }

    [MemoryPackOrder(7)]
    public Guid WalletId { get; set; }

    [MemoryPackOrder(8)]
    public Guid? GatewayId { get; set; }

    [MemoryPackOrder(9)]
    public WalletWorkflowStatus WorkflowStatus { get; set; } = WalletWorkflowStatus.PendingValidation;

    [MemoryPackOrder(10)]
    public Guid? ApprovalId { get; set; }

    [MemoryPackOrder(11)]
    public Guid? HoldOperationId { get; set; }

    [MemoryPackOrder(12)]
    public Guid? SettlementOperationId { get; set; }

    [MemoryPackOrder(13)]
    public Guid? SettlementTransactionId { get; set; }

    [MemoryPackOrder(14)]
    public string? ExternalReference { get; set; }

    [MemoryPackOrder(15)]
    public string? ProviderEventId { get; set; }

    [MemoryPackOrder(16)]
    public string? ProviderTransactionId { get; set; }

    [MemoryPackOrder(17)]
    public string? ProviderStatus { get; set; }

    [MemoryPackOrder(18)]
    public decimal? RequestedFee { get; set; }

    [MemoryPackOrder(19)]
    public decimal? CalculatedFee { get; set; }

    [MemoryPackOrder(20)]
    public string? RawRequestData { get; set; }

    [MemoryPackOrder(21)]
    public string? RawResponseData { get; set; }

    [MemoryPackOrder(22)]
    public Guid? RequestedByCredentialId { get; set; }

    [MemoryPackOrder(23)]
    public Guid? ApprovedByCredentialId { get; set; }

    [MemoryPackOrder(24)]
    public DateTime? ApprovedAt { get; set; }

    [MemoryPackOrder(25)]
    public DateTime? SettledAt { get; set; }

    [MemoryPackOrder(26)]
    public DateTime? FailedAt { get; set; }

    [MemoryPackOrder(27)]
    public DateTime? CancelledAt { get; set; }

    [MemoryPackOrder(28)]
    public string? FailureReason { get; set; }

    [MemoryPackOrder(29)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(30)]
    public virtual Wallet? Wallet { get; set; }

    [MemoryPackOrder(31)]
    public virtual PaymentGateway? PaymentGateway { get; set; }

    [MemoryPackOrder(32)]
    public virtual WalletApprovalRequest? Approval { get; set; }

    [MemoryPackOrder(33)]
    public virtual WalletOperation? HoldOperation { get; set; }

    [MemoryPackOrder(34)]
    public virtual WalletOperation? SettlementOperation { get; set; }

    [MemoryPackOrder(35)]
    public virtual WalletTransaction? SettlementTransaction { get; set; }

    [MemoryPackOrder(36)]
    public string? IdempotencyKey { get; set; }

    [MemoryPackOrder(37)]
    public string? RequestHash { get; set; }
}

public class CreateWithdrawalRequestRequest
{
    public Guid CredentialId { get; set; }
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Fee { get; set; }
    public TransactionStatus WithdrawalStatus { get; set; }
    public string? Remarks { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid WalletId { get; set; }
}

public class UpdateWithdrawalRequestRequest
{
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Fee { get; set; }
    public TransactionStatus WithdrawalStatus { get; set; }
    public string? Remarks { get; set; }
    public string? ReferenceNumber { get; set; }
}

public class GetWithdrawalRequestListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public Guid? WalletId { get; set; }
    public TransactionStatus? WithdrawalStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
