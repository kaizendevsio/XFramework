using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity
{
    public class DeleteIdentityWalletRequest : RequestBase
    {
        public long UserAuthId { get; set; }
        public long? WalletTypeId { get; set; }
        
    }
}