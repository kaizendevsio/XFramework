using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Create;

public class CreateWalletEntityRequest : RequestBase
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public short Type { get; set; }
    public Guid? CurrencyEntityGuid { get; set; }
    public decimal? MinTransfer { get; set; }
    public decimal? MaxTransfer { get; set; }
}