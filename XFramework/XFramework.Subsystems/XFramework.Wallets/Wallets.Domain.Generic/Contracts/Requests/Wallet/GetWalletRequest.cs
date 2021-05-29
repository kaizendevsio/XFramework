namespace Wallets.Domain.Generic.Contracts.Requests.Wallet
{
    public class GetWalletRequest 
    {
        public long Id { get; set; }
        public string Code { get; set; }
    }
}