using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Get;

public class GetWalletListRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}