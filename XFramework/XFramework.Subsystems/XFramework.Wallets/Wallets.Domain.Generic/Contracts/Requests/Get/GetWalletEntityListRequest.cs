using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Get;

public class GetWalletEntityListRequest : RequestBase
{
    public Guid? ApplicationGuid { get; set; }
}