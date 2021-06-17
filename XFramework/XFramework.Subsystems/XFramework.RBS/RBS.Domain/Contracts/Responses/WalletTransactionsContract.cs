using System;
using System.Collections.Generic;
using RBS.Domain.BusinessObjects;

namespace RBS.Domain.Contracts.Responses
{
    public class WalletTransactionContract
    {
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long UserAuthId { get; set; }
        public long? SourceUserWalletId { get; set; }
        public decimal? Amount { get; set; }
        public string Remarks { get; set; }
        public decimal? RunningBalance { get; set; }
        public string Description { get; set; }
    }
}