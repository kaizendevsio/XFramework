using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class DepositRequest : BaseModel
{
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


    public virtual ICollection<BusinessPackage> BusinessPackages { get; set; } = new List<BusinessPackage>();

    public virtual PaymentGateway? PaymentGateway { get; set; }

    public virtual IdentityCredential? Credential { get; set; }

    public virtual CurrencyType? SourceCurrency { get; set; }

    public virtual WalletType? WalletType { get; set; }
}