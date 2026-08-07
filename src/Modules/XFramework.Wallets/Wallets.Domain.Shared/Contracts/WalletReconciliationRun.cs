using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-reconciliation-runs",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets.reconciliation",
    ReadCapability = "manage",
    CacheDurationSeconds = 30,
    CacheKeyPrefix = "wallet-reconciliation-runs"
)]
public partial class WalletReconciliationRun : BaseModel
{
    [MemoryPackOrder(0)]
    public WalletReconciliationStatus Status { get; set; } = WalletReconciliationStatus.Pending;

    [MemoryPackOrder(1)]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [MemoryPackOrder(2)]
    public DateTime? CompletedAt { get; set; }

    [MemoryPackOrder(3)]
    public int CheckedCount { get; set; }

    [MemoryPackOrder(4)]
    public int DriftCount { get; set; }

    [MemoryPackOrder(5)]
    public string? Error { get; set; }

    [MemoryPackOrder(6)]
    public virtual ICollection<WalletReconciliationItem> Items { get; set; } = [];
}

public class GetWalletReconciliationRunListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public WalletReconciliationStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
