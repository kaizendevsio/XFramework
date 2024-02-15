namespace XFramework.Domain.Generic.Contracts;

public partial class Wallet : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid? WalletTypeId { get; set; }

    public decimal? Balance { get; set; }


    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual ICollection<WalletAddress> WalletAddresses { get; set; } = new List<WalletAddress>();

    public virtual WalletType? WalletType { get; set; }

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
}