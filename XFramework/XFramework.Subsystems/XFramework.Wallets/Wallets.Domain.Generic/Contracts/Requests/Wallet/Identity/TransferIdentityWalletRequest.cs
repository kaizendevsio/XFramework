using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity
{
    public class TransferIdentityWalletRequest : RequestBase
    {
        public long? FromWalletTypeId { get; set; }
        public long? ToWalletTypeId { get; set; }
        public decimal? Amount { get; set; }
        public long? FromUserCredentialId { get; set; }
        public long? ToUserCredentialId { get; set; }
    }
}