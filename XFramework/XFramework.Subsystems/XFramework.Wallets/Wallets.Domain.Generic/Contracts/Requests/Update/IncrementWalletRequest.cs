using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Update;

public class IncrementWalletRequest: RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? WalletEntityGuid { get; set; }
    public decimal? Amount { get; set; }
       
}