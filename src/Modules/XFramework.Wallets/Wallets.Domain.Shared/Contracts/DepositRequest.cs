using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/deposit-requests",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "deposit-requests"
)]
public partial class DepositRequest : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? SourceCurrencyId { get; set; }

    [MemoryPackOrder(2)]
    public Guid? WalletTypeId { get; set; }

    [MemoryPackOrder(3)]
    public string? Address { get; set; }

    [MemoryPackOrder(4)]
    public decimal? Amount { get; set; }

    [MemoryPackOrder(5)]
    public string? Remarks { get; set; }

    [MemoryPackOrder(6)]
    public short? DepositStatus { get; set; }

    [MemoryPackOrder(7)]
    public DateTime? ExpiryDate { get; set; }

    [MemoryPackOrder(8)]
    public string? RawRequestData { get; set; }

    [MemoryPackOrder(9)]
    public string? ReferenceNo { get; set; }

    [MemoryPackOrder(10)]
    public string? RawResponseData { get; set; }

    [MemoryPackOrder(11)]
    public decimal? Discount { get; set; }

    [MemoryPackOrder(12)]
    public decimal? ConvenienceFee { get; set; }

    [MemoryPackOrder(13)]
    public decimal? SystemFee { get; set; }

    [MemoryPackOrder(14)]
    public int? DiscountType { get; set; }

    [MemoryPackOrder(15)]
    public Guid GatewayId { get; set; }


    [MemoryPackOrder(16)]
    public virtual PaymentGateway? PaymentGateway { get; set; }

    [MemoryPackOrder(17)]
    public virtual IdentityCredential? Credential { get; set; }

    [MemoryPackOrder(18)]
    public virtual CurrencyType? SourceCurrency { get; set; }

    [MemoryPackOrder(19)]
    public virtual WalletType? WalletType { get; set; }
}

public class CreateDepositRequestRequest
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
    public decimal? Discount { get; set; }
    public decimal? ConvenienceFee { get; set; }
    public decimal? SystemFee { get; set; }
    public int? DiscountType { get; set; }
    public Guid GatewayId { get; set; }
}

public class UpdateDepositRequestRequest
{
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public string? Remarks { get; set; }
    public short? DepositStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? RawResponseData { get; set; }
    public decimal? Discount { get; set; }
    public decimal? ConvenienceFee { get; set; }
    public decimal? SystemFee { get; set; }
}

public class GetDepositRequestListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public Guid? WalletTypeId { get; set; }
    public Guid? SourceCurrencyId { get; set; }
    public short? DepositStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
