namespace XFramework.Domain.Generic.Contracts;

public partial class WithdrawalRequest : BaseModel
{
    public Guid CredentialId { get; set; }

    public string? Address { get; set; }

    public decimal? TotalAmount { get; set; }

    public short? WithdrawalStatus { get; set; }

    public string? Remarks { get; set; }

    public Guid WalletId { get; set; }

    public Guid WalletTypeId { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual WalletType? WalletType { get; set; }

    public virtual Wallet? Wallet { get; set; }
}