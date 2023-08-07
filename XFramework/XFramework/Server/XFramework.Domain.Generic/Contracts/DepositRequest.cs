namespace XFramework.Domain.Generic.Contracts;

public partial class DepositRequest
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public Guid CredentialId { get; set; }

    public Guid? SourceCurrencyId { get; set; }

    public Guid? WalletTypeId { get; set; }

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

    public Guid GatewayId { get; set; }

    
    public virtual ICollection<BusinessPackage> BusinessPackages { get; } = new List<BusinessPackage>();

    public virtual PaymentGateway? PaymentGateway { get; set; }

    public virtual IdentityCredential? Credential { get; set; }

    public virtual CurrencyType? SourceCurrency { get; set; }

    public virtual WalletType? WalletType { get; set; }
}
