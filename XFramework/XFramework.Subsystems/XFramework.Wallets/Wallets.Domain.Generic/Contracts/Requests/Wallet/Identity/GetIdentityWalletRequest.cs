namespace Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity
{
    public class GetIdentityWalletRequest
    {
        public long UserAuthId { get; set; }
        public long? WalletTypeId { get; set; }
    }
}