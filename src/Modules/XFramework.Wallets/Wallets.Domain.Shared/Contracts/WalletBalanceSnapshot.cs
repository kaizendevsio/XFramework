using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-balance-snapshots",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets.reporting",
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "wallet-balance-snapshots"
)]
public partial class WalletBalanceSnapshot : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid WalletId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? WalletTypeId { get; set; }

    [MemoryPackOrder(2)]
    public Guid? CurrencyId { get; set; }

    [MemoryPackOrder(3)]
    public decimal Balance { get; set; }

    [MemoryPackOrder(4)]
    public decimal AvailableBalance { get; set; }

    [MemoryPackOrder(5)]
    public decimal TransferableBalance { get; set; }

    [MemoryPackOrder(6)]
    public decimal DebitOnHoldBalance { get; set; }

    [MemoryPackOrder(7)]
    public decimal CreditOnHoldBalance { get; set; }

    [MemoryPackOrder(8)]
    public decimal TotalBalance { get; set; }

    [MemoryPackOrder(9)]
    public Guid? LastOperationId { get; set; }

    [MemoryPackOrder(10)]
    public Guid? LastLedgerEntryId { get; set; }

    [MemoryPackOrder(11)]
    public bool IsReconciled { get; set; } = true;

    [MemoryPackOrder(12)]
    public decimal DriftAmount { get; set; }

    [MemoryPackOrder(13)]
    public DateTime? ReconciledAt { get; set; }

    [MemoryPackOrder(14)]
    public virtual Wallet Wallet { get; set; } = null!;

    [MemoryPackOrder(15)]
    public virtual WalletOperation? LastOperation { get; set; }

    [MemoryPackOrder(16)]
    public virtual WalletLedgerEntry? LastLedgerEntry { get; set; }
}

public class GetWalletBalanceSnapshotListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? WalletId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public bool? IsReconciled { get; set; }
}
