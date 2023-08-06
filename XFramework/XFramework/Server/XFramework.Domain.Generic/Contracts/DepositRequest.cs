namespace XFramework.Domain.Generic.Contracts;

public partial class DepositRequest
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public long? IdentityCredentialId { get; set; }

    public long? SourceCurrencyId { get; set; }

    public Guid? TargetWalletTypeId { get; set; }

    public string? Address { get; set; }

    public decimal? Amount { get; set; }

    public string? Remarks { get; set; }

    public short? DepositStatus { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? RawRequestData { get; set; }

    public string? ReferenceNo { get; set; }

    public string? RawResponseData { get; set; }

    public decimal? Discount { get; set; }

    public decimal? ConvenienceFee { get; set; }

    public decimal? SystemFee { get; set; }

    public int? DiscountType { get; set; }

    public long? GatewayId { get; set; }

    
    public virtual ICollection<BusinessPackage> BusinessPackages { get; } = new List<BusinessPackage>();

    public virtual PaymentGateway? Gateway { get; set; }

    public virtual IdentityCredential? IdentityCredential { get; set; }

    public virtual CurrencyType? SourceCurrency { get; set; }

    public virtual WalletType? TargetWalletType { get; set; }
}
