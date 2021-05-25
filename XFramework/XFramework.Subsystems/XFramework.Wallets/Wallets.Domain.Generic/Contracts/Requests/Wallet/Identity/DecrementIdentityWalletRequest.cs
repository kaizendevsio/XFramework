using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity
{
    public class DecrementIdentityWalletRequest: RequestBase
    {
        public long UserAuthId { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? Amount { get; set; }
    }
}