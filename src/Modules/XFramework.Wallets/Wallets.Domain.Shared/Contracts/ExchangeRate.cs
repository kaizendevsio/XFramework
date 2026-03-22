using XFramework.Domain.Shared.Attributes;

namespace Wallets.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/exchange-rates",
    RequireAuthorization = true,
    CacheDurationSeconds = 1800,
    CacheKeyPrefix = "exchange-rates"
)]
public partial class ExchangeRate : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid SourceCurrencyTypeId { get; set; }

    [MemoryPackOrder(1)]
    public Guid TargetCurrencyTypeId { get; set; }

    [MemoryPackOrder(2)]
    public decimal? Value { get; set; }

    [MemoryPackOrder(3)]
    public decimal? Fee { get; set; }

    [MemoryPackOrder(4)]
    public DateTime? EffectivityDate { get; set; }

    [MemoryPackOrder(5)]
    public DateTime? ExpiryDate { get; set; }

    [MemoryPackOrder(6)]
    public virtual CurrencyType SourceCurrencyType { get; set; } = null!;

    [MemoryPackOrder(7)]
    public virtual CurrencyType TargetCurrencyType { get; set; } = null!;
}

public class CreateExchangeRateRequest
{
    public Guid SourceCurrencyTypeId { get; set; }
    public Guid TargetCurrencyTypeId { get; set; }
    public decimal? Value { get; set; }
    public decimal? Fee { get; set; }
    public DateTime? EffectivityDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class UpdateExchangeRateRequest
{
    public decimal? Value { get; set; }
    public decimal? Fee { get; set; }
    public DateTime? EffectivityDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class GetExchangeRateListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? SourceCurrencyTypeId { get; set; }
    public Guid? TargetCurrencyTypeId { get; set; }
    public DateTime? EffectiveOn { get; set; }
}
