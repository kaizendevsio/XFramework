namespace XFramework.Domain.Generic.Contracts;

public partial class Wallet
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public Guid CredentialId { get; set; }

    public Guid? WalletTypeId { get; set; }

    public decimal? Balance { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual ICollection<WalletAddress> WalletAddresses { get; } = new List<WalletAddress>();

    public virtual WalletType? WalletType { get; set; }

    public virtual ICollection<WalletTransaction> WalletTransactions { get; } = new List<WalletTransaction>();
    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; } = new List<WithdrawalRequest>();

}
