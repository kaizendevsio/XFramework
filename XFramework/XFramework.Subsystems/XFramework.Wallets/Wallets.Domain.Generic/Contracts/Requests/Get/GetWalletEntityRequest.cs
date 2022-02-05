using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Get;

public class GetWalletEntityRequest : RequestBase
{
    public Guid? Guid { get; set; }
}