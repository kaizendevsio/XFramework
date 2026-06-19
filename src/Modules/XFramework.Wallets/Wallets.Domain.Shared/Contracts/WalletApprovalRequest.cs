using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-approval-requests",
    RequireAuthorization = true,
    CacheDurationSeconds = 30,
    CacheKeyPrefix = "wallet-approval-requests"
)]
public partial class WalletApprovalRequest : BaseModel
{
    [MemoryPackOrder(0)]
    public WalletOperationType OperationType { get; set; }

    [MemoryPackOrder(1)]
    public WalletApprovalStatus Status { get; set; } = WalletApprovalStatus.Pending;

    [MemoryPackOrder(2)]
    public Guid? WalletId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? OperationId { get; set; }

    [MemoryPackOrder(4)]
    public Guid RequesterCredentialId { get; set; }

    [MemoryPackOrder(5)]
    public Guid? ApproverCredentialId { get; set; }

    [MemoryPackOrder(6)]
    public decimal? Amount { get; set; }

    [MemoryPackOrder(7)]
    public string? Reason { get; set; }

    [MemoryPackOrder(8)]
    public string? DecisionReason { get; set; }

    [MemoryPackOrder(9)]
    public string? AuditMetadataJson { get; set; }

    [MemoryPackOrder(10)]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [MemoryPackOrder(11)]
    public DateTime? DecidedAt { get; set; }

    [MemoryPackOrder(12)]
    public virtual Wallet? Wallet { get; set; }

    [MemoryPackOrder(13)]
    public virtual WalletOperation? Operation { get; set; }
}

public class GetWalletApprovalRequestListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public WalletOperationType? OperationType { get; set; }
    public WalletApprovalStatus? Status { get; set; }
    public Guid? WalletId { get; set; }
    public Guid? RequesterCredentialId { get; set; }
    public Guid? ApproverCredentialId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
