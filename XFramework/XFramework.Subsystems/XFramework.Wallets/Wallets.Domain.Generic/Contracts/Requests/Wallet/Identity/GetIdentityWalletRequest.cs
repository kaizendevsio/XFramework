namespace Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity
{
    public class GetIdentityWalletRequest
    {
        public long Cuid { get; set; }
        public long? WalletTypeId { get; set; }
    }
}