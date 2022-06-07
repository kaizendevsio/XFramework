using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Update;

public class UpdateExchangeRateRequest : TransactionRequestBase
{
    public Guid? SourceCurrencyEntityGuid { get; set; }
    public Guid? TargetCurrencyEntityGuid { get; set; }
    public decimal? Value { get; set; }
    public decimal? Fee { get; set; }
    public DateTime? EffectivityDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
 
}