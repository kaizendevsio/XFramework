namespace Wallets.Domain.Generic.Contracts.Responses;

public class ExchangeRateResponse
{
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public Guid? SourceCurrencyEntityGuid { get; set; }
    public Guid? TargetCurrencyEntityGuid { get; set; }
    public decimal? Value { get; set; }
    public decimal? Fee { get; set; }
    public DateTime? EffectivityDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}