namespace XFramework.Domain.Generic.Contracts;

public partial class WalletTransaction
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public long IdentityCredentialId { get; set; }

    public long? SourceUserWalletId { get; set; }

    public decimal? Amount { get; set; }

    public string? Remarks { get; set; }

    public decimal? RunningBalance { get; set; }

    public string? Description { get; set; }

    public long? TargetUserWalletId { get; set; }

    public decimal? PreviousBalance { get; set; }

    
    public virtual IdentityCredential IdentityCredential { get; set; } = null!;

    public virtual Wallet? SourceUserWallet { get; set; }

    public virtual Wallet? TargetUserWallet { get; set; }
}
