using System;

namespace RBS.Domain.Contracts.Responses
{
    public class WalletDetailsContract
    {
        public DateTime? ModifiedAt { get; set; }
        public long? WalletTypeId { get; set; }
        public string WalletCode { get; set; }
        public string WalletName { get; set; }
        
        public decimal? Balance { get; set; }
    }
}