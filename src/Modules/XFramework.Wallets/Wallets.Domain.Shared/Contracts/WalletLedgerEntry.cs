using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/wallet-ledger-entries",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets.reporting",
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "wallet-ledger-entries"
)]
public partial class WalletLedgerEntry : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid OperationId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? WalletId { get; set; }

    [MemoryPackOrder(2)]
    public Guid? WalletTransactionId { get; set; }

    [MemoryPackOrder(3)]
    public Guid? CurrencyId { get; set; }

    [MemoryPackOrder(4)]
    public Guid? WalletTypeId { get; set; }

    [MemoryPackOrder(5)]
    public WalletLedgerDirection Direction { get; set; }

    [MemoryPackOrder(6)]
    public WalletBalanceBucket BalanceBucket { get; set; }

    [MemoryPackOrder(7)]
    public WalletLedgerEntryKind EntryKind { get; set; } = WalletLedgerEntryKind.Principal;

    [MemoryPackOrder(8)]
    public decimal Amount { get; set; }

    [MemoryPackOrder(9)]
    public int Sequence { get; set; }

    [MemoryPackOrder(10)]
    public string? Description { get; set; }

    [MemoryPackOrder(11)]
    public string? ReferenceNumber { get; set; }

    [MemoryPackOrder(12)]
    public string? CounterpartyType { get; set; }

    [MemoryPackOrder(13)]
    public string? CounterpartyReference { get; set; }

    [MemoryPackOrder(14)]
    public decimal? PreviousBalance { get; set; }

    [MemoryPackOrder(15)]
    public decimal? PreviousAvailableBalance { get; set; }

    [MemoryPackOrder(16)]
    public decimal? PreviousDebitOnHoldBalance { get; set; }

    [MemoryPackOrder(17)]
    public decimal? PreviousCreditOnHoldBalance { get; set; }

    [MemoryPackOrder(18)]
    public decimal? RunningBalance { get; set; }

    [MemoryPackOrder(19)]
    public decimal? RunningAvailableBalance { get; set; }

    [MemoryPackOrder(20)]
    public decimal? RunningDebitOnHoldBalance { get; set; }

    [MemoryPackOrder(21)]
    public decimal? RunningCreditOnHoldBalance { get; set; }

    [MemoryPackOrder(22)]
    public virtual WalletOperation Operation { get; set; } = null!;

    [MemoryPackOrder(23)]
    public virtual Wallet? Wallet { get; set; }

    [MemoryPackOrder(24)]
    public virtual WalletTransaction? WalletTransaction { get; set; }
}

public class GetWalletLedgerEntryListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? OperationId { get; set; }
    public Guid? WalletId { get; set; }
    public Guid? WalletTransactionId { get; set; }
    public WalletLedgerDirection? Direction { get; set; }
    public WalletBalanceBucket? BalanceBucket { get; set; }
    public WalletLedgerEntryKind? EntryKind { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
