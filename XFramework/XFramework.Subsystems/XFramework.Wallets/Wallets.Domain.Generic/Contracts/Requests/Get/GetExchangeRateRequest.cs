using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Get;

public class GetExchangeRateRequest : RequestBase
{
    public Guid? SourceCurrencyEntityGuid { get; set; }
    public Guid? TargetCurrencyEntityGuid { get; set; }
}