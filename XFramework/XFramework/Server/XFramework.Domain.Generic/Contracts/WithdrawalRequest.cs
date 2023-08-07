namespace XFramework.Domain.Generic.Contracts;

public partial class WithdrawalRequest
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

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
