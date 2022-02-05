using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Delete;

public class DeleteWalletEntityRequest : RequestBase
{
    public Guid? Guid { get; set; }
}