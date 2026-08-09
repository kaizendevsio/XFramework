using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/currencies",
    RequireAuthorization = true,
    AuthorizationFeature = "wallets",
    CacheDurationSeconds = 3600,
    CacheKeyPrefix = "currencies"
)]
public partial class CurrencyType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }

    [MemoryPackOrder(1)]
    public string? CurrencyIsoCode3 { get; set; }

    [MemoryPackOrder(2)]
    public string? Description { get; set; }

    [MemoryPackOrder(3)]
    public short? Type { get; set; }

    [MemoryPackOrder(4)]
    public virtual ICollection<AddressCountry> AddressCountries { get; set; } = new List<AddressCountry>();

    [MemoryPackOrder(5)]
    public virtual ICollection<DepositRequest> DepositRequests { get; set; } = new List<DepositRequest>();

    [MemoryPackOrder(6)]
    public virtual ICollection<ExchangeRate> ExchangeRateSourceCurrencyTypes { get; set; } = new List<ExchangeRate>();

    [MemoryPackOrder(7)]
    public virtual ICollection<ExchangeRate> ExchangeRateTargetCurrencyTypes { get; set; } = new List<ExchangeRate>();

    [MemoryPackOrder(8)]
    public virtual ICollection<WalletType> WalletTypes { get; set; } = new List<WalletType>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}

public class CreateCurrencyTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string CurrencyIsoCode3 { get; set; } = string.Empty;
    public string? Description { get; set; }
    public short? Type { get; set; }
    public Guid SystemReferenceId { get; set; }
}

public class UpdateCurrencyTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string CurrencyIsoCode3 { get; set; } = string.Empty;
    public string? Description { get; set; }
    public short? Type { get; set; }
}

public class GetCurrencyTypeListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public short? Type { get; set; }
}
