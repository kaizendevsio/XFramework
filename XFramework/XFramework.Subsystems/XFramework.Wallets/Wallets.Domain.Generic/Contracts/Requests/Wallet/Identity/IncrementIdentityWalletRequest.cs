using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity
{
    public class IncrementIdentityWalletRequest: RequestBase
    {
        public long UserCredentialId { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? Amount { get; set; }
       
    }
}