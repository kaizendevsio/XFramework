using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-reconciliation-items",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets.reconciliation",
    ReadCapability = "manage",
    CacheDurationSeconds = 30,
    CacheKeyPrefix = "wallet-reconciliation-items"
)]
public partial class WalletReconciliationItem : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid RunId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? WalletId { get; set; }

    [MemoryPackOrder(2)]
    public string CheckType { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public WalletReconciliationStatus Status { get; set; } = WalletReconciliationStatus.Pending;

    [MemoryPackOrder(4)]
    public decimal ExpectedAmount { get; set; }

    [MemoryPackOrder(5)]
    public decimal ActualAmount { get; set; }

    [MemoryPackOrder(6)]
    public decimal DriftAmount { get; set; }

    [MemoryPackOrder(7)]
    public string? ReferenceNumber { get; set; }

    [MemoryPackOrder(8)]
    public string? DetailsJson { get; set; }

    [MemoryPackOrder(9)]
    public string? RepairSuggestion { get; set; }

    [MemoryPackOrder(10)]
    public Guid? MarkedReconciledByCredentialId { get; set; }

    [MemoryPackOrder(11)]
    public DateTime? MarkedReconciledAt { get; set; }

    [MemoryPackOrder(12)]
    public virtual WalletReconciliationRun Run { get; set; } = null!;

    [MemoryPackOrder(13)]
    public virtual Wallet? Wallet { get; set; }
}

public class GetWalletReconciliationItemListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? RunId { get; set; }
    public Guid? WalletId { get; set; }
    public string? CheckType { get; set; }
    public WalletReconciliationStatus? Status { get; set; }
}
