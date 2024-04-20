using System;

namespace Coins.Web.Models
{
    public class TransactionVM
    {
        public long Id { get; set; }
        public string BtcAddress { get; set; }
        public Decimal BtcAmount { get; set; }
        public string Name { get; set; }
        public DateTime DateTime { get; set; } = DateTime.Now;
    }
}