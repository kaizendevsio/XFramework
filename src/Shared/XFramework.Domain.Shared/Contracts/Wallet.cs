namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class Wallet : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? WalletTypeId { get; set; }

    [MemoryPackOrder(2)]
    public decimal Balance { get; set; }
    
    [MemoryPackOrder(3)]
    public string? AccountNumber { get; set; }
    
    [MemoryPackOrder(4)]
    public int CardNumber { get; set; }
    
    [MemoryPackOrder(5)]
    public decimal DebitOnHoldBalance { get; set; }
    
    [MemoryPackOrder(6)]
    public decimal CreditOnHoldBalance { get; set; }
    
    [MemoryPackOrder(7)]
    public decimal TransferableBalance { get; set; }
    
    [MemoryPackOrder(8)]
    public decimal? MinTransferRule { get; set; }

    [MemoryPackOrder(9)]
    public decimal? MaxTransferRule { get; set; }
    
    [MemoryPackOrder(10)]
    public decimal? BondBalanceRule { get; set; }
    
    [MemoryPackOrder(11)]
    public decimal? MaintainingBalanceRule { get; set; }
    
    [MemoryPackIgnore]
    public decimal? TotalBalance => Balance + CreditOnHoldBalance - DebitOnHoldBalance; // Total funds in the wallet
    
    [MemoryPackIgnore]
    public decimal AvailableBalance => Balance - DebitOnHoldBalance; // Funds that are free to be spent or withdrawn now

    [MemoryPackOrder(12)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(13)]
    public virtual ICollection<WalletAddress> WalletAddresses { get; set; } = new List<WalletAddress>();

    [MemoryPackOrder(14)]
    public virtual WalletType? WalletType { get; set; }

    [MemoryPackOrder(15)]
    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    [MemoryPackOrder(16)]
    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
}
