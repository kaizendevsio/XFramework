namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class WalletType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Code { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(2)]
    public string? Desc { get; set; }

    [MemoryPackOrder(3)]
    public short Type { get; set; }

    [MemoryPackOrder(4)]
    public Guid? CurrencyTypeId { get; set; }

    [MemoryPackOrder(5)]
    public decimal? MinTransferRule { get; set; }

    [MemoryPackOrder(6)]
    public decimal? MaxTransferRule { get; set; }
    
    [MemoryPackOrder(7)]
    public decimal? BondBalanceRule { get; set; }
    
    [MemoryPackOrder(8)]
    public decimal? MaintainingBalanceRule { get; set; }

    [MemoryPackOrder(9)]
    public virtual Tenant Tenant { get; set; } = null!;

    [MemoryPackOrder(10)]
    public virtual CurrencyType? CurrencyType { get; set; }

    [MemoryPackOrder(11)]
    public virtual ICollection<DepositRequest> DepositRequests { get; set; } = new List<DepositRequest>();
    [MemoryPackOrder(12)]
    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();

    [MemoryPackOrder(13)]
    public virtual ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
}
