namespace XFramework.Domain.Generic.Contracts;

public partial class WalletTransaction : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid WalletId { get; set; }

    public decimal Amount { get; set; }

    public string? Remarks { get; set; }

    public decimal? RunningBalance { get; set; }

    public string? Description { get; set; }

    public decimal PreviousBalance { get; set; }


    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual Wallet? Wallet { get; set; }
}