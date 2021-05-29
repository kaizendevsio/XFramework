using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Wallet
{
    public class CreateWalletRequest : RequestBase
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public short Type { get; set; }
        public long? CurrencyEntityId { get; set; }
        public decimal? MinTransfer { get; set; }
        public decimal? MaxTransfer { get; set; }
    }
}