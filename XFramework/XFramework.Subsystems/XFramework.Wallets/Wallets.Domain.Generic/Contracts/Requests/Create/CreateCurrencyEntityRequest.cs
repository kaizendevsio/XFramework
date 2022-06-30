using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Create;

public class CreateCurrencyEntityRequest : TransactionRequestBase
{
    public string Name { get; set; }
    public string CurrencyIsoCode3 { get; set; }
    public string Description { get; set; }
    public short? CurrencyType { get; set; }
}