using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity
{
    public class CreateIdentityWalletRequest : RequestBase
    {
        public long UserAuthId { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? Balance { get; set; }
    }
}