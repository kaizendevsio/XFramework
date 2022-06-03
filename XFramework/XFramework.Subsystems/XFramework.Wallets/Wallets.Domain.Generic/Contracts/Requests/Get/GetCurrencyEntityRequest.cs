using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Get;

public class GetCurrencyEntityRequest : RequestBase
{
    public Guid? CurrencyEntityGuid { get; set; }
}