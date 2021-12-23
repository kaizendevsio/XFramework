using System.Collections.Generic;

namespace RBS.Domain.Contracts.Responses
{
    public class WalletListContract
    {
        public List<WalletDetailsContract> Wallets { get; set; }
    }
}