using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Update;

public class UpdateWalletEntityRequest : RequestBase  
{
    public Guid? Guid { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Desc { get; set; }
    public short Type { get; set; }
    public Guid? CurrencyEntityGuid { get; set; }
    public decimal? MinTransfer { get; set; }
    public decimal? MaxTransfer { get; set; }
}