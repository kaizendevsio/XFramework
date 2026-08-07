using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-operations",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets.reporting",
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "wallet-operations"
)]
public partial class WalletOperation : BaseModel
{
    [MemoryPackOrder(0)]
    public WalletOperationType OperationType { get; set; }

    [MemoryPackOrder(1)]
    public WalletOperationStatus Status { get; set; } = WalletOperationStatus.Pending;

    [MemoryPackOrder(2)]
    public string? IdempotencyKey { get; set; }

    [MemoryPackOrder(3)]
    public string? RequestHash { get; set; }

    [MemoryPackOrder(4)]
    public string? ReferenceNumber { get; set; }

    [MemoryPackOrder(5)]
    public string? CorrelationId { get; set; }

    [MemoryPackOrder(6)]
    public Guid? ActorCredentialId { get; set; }

    [MemoryPackOrder(7)]
    public string? ExternalReference { get; set; }

    [MemoryPackOrder(8)]
    public string? RiskDecision { get; set; }

    [MemoryPackOrder(9)]
    public string? PolicyDecision { get; set; }

    [MemoryPackOrder(10)]
    public string? Reason { get; set; }

    [MemoryPackOrder(11)]
    public string? FailureMessage { get; set; }

    [MemoryPackOrder(12)]
    public DateTime? CompletedAt { get; set; }

    [MemoryPackOrder(13)]
    public decimal? RequestedFee { get; set; }

    [MemoryPackOrder(14)]
    public decimal? CalculatedFee { get; set; }

    [MemoryPackOrder(15)]
    public bool RequiresApproval { get; set; }

    [MemoryPackOrder(16)]
    public Guid? ApprovalId { get; set; }

    [MemoryPackOrder(17)]
    public Guid? OriginalOperationId { get; set; }

    [MemoryPackOrder(18)]
    public string? PolicyDecisionJson { get; set; }

    [MemoryPackOrder(19)]
    public string? RiskTier { get; set; }

    [MemoryPackOrder(20)]
    public decimal? RiskScore { get; set; }

    [MemoryPackOrder(21)]
    public virtual ICollection<WalletLedgerEntry> LedgerEntries { get; set; } = [];

    [MemoryPackOrder(22)]
    public virtual ICollection<WalletOutboxMessage> OutboxMessages { get; set; } = [];

    [MemoryPackOrder(23)]
    public virtual WalletApprovalRequest? Approval { get; set; }

    [MemoryPackOrder(24)]
    public virtual WalletOperation? OriginalOperation { get; set; }
}

public class GetWalletOperationListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? ActorCredentialId { get; set; }
    public WalletOperationType? OperationType { get; set; }
    public WalletOperationStatus? Status { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
