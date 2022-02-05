using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Get;

public class GetWalletRequest : RequestBase
{
    public Guid? Guid { get; set; }
}