namespace XFramework.Domain.Generic.Contracts;

public partial class WithdrawalRequest
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public long IdentityCredentialId { get; set; }

    public string? Address { get; set; }

    public decimal? TotalAmount { get; set; }

    public short? WithdrawalStatus { get; set; }

    public string? Remarks { get; set; }

    public Guid? SourceWalletTypeId { get; set; }

    public long? TargetCurrencyId { get; set; }

    
    public virtual IdentityCredential IdentityCredential { get; set; } = null!;

    public virtual WalletType? SourceWalletType { get; set; }

    public virtual WalletType? TargetCurrency { get; set; }
}
