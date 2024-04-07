namespace XFramework.Domain.Generic.Contracts;

public partial class Wallet : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid? WalletTypeId { get; set; }

    public decimal Balance { get; set; }
    
    public string? AccountNumber { get; set; }
    
    public int CardNumber { get; set; }
    
    public decimal DebitOnHoldBalance { get; set; }
    public decimal CreditOnHoldBalance { get; set; }
    
    public decimal TransferableBalance { get; set; }
    
    public decimal? MinTransferRule { get; set; }

    public decimal? MaxTransferRule { get; set; }
    
    public decimal? BondBalanceRule { get; set; }
    
    public decimal? MaintainingBalanceRule { get; set; }
    
    public decimal? TotalBalance => Balance + CreditOnHoldBalance - DebitOnHoldBalance; // Total funds in the wallet
    
    public decimal AvailableBalance => Balance - DebitOnHoldBalance; // Funds that are free to be spent or withdrawn now

    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual ICollection<WalletAddress> WalletAddresses { get; set; } = new List<WalletAddress>();

    public virtual WalletType? WalletType { get; set; }

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
}