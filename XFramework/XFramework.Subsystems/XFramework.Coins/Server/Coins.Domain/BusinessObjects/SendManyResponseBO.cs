using System.Collections.Generic;

namespace Coins.Domain.BusinessObjects
{
    public class SendManyResponseBO
    {
        public List<long> IDs { get; set; }
        public List<string> To { get; set; }
        public List<decimal> Amounts { get; set; }
        public List<string> From { get; set; }
        public decimal Fee { get; set; }
        public string Txid { get; set; }
        public string Tx_Hash { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; }
        public string Warning { get; set; }

        public SendManyResponseBO()
        {
            IDs = new List<long>();
            To = new List<string>();
            Amounts = new List<decimal>();
            From = new List<string>();
        }
    }
}