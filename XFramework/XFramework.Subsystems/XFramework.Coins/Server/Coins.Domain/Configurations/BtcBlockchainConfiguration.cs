using System;

namespace Coins.Domain.Configurations
{
    public class BtcBlockchainConfiguration
    {
        public WalletConfiguration WalletConfiguration { get; set; } = new();
        
        public Uri ApiUrl { get; set; }
        public Uri ServiceUrl { get; set; }
        public Uri PaymentCallbackUrl { get; set; }
        public Uri FeeUrl { get; set; }
        
        public string ApiCode { get; set; }
        public int MinConfirmations { get; set; }
        public int MaxGapLimit { get; set; }
        public decimal GapPaymentAmount { get; set; }
    }

    public class WalletConfiguration
    {
        public string PublicKey { get; set; }
        public string WalletIdentifier { get; set; }
        public string WalletPassword { get; set; }
    }
}