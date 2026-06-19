using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-cases",
    RequireAuthorization = true,
    CacheDurationSeconds = 30,
    CacheKeyPrefix = "wallet-cases"
)]
public partial class WalletCase : BaseModel
{
    [MemoryPackOrder(0)]
    public WalletCaseType CaseType { get; set; }

    [MemoryPackOrder(1)]
    public WalletCaseStatus Status { get; set; } = WalletCaseStatus.Open;

    [MemoryPackOrder(2)]
    public Guid WalletId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? OriginalOperationId { get; set; }

    [MemoryPackOrder(4)]
    public Guid? OriginalTransactionId { get; set; }

    [MemoryPackOrder(5)]
    public Guid? SettlementOperationId { get; set; }

    [MemoryPackOrder(6)]
    public decimal Amount { get; set; }

    [MemoryPackOrder(7)]
    public string? ExternalReference { get; set; }

    [MemoryPackOrder(8)]
    public string? ReasonCode { get; set; }

    [MemoryPackOrder(9)]
    public string? Reason { get; set; }

    [MemoryPackOrder(10)]
    public Guid RequesterCredentialId { get; set; }

    [MemoryPackOrder(11)]
    public Guid? DeciderCredentialId { get; set; }

    [MemoryPackOrder(12)]
    public DateTime? ResolvedAt { get; set; }

    [MemoryPackOrder(13)]
    public virtual Wallet Wallet { get; set; } = null!;

    [MemoryPackOrder(14)]
    public virtual WalletOperation? OriginalOperation { get; set; }

    [MemoryPackOrder(15)]
    public virtual WalletOperation? SettlementOperation { get; set; }

    [MemoryPackOrder(16)]
    public virtual WalletTransaction? OriginalTransaction { get; set; }
}

public class GetWalletCaseListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public WalletCaseType? CaseType { get; set; }
    public WalletCaseStatus? Status { get; set; }
    public Guid? WalletId { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
