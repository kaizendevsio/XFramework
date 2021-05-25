using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Wallet
{
    public class DeleteWalletRequest : RequestBase
    {
        public long Id { get; set; }
    }
}