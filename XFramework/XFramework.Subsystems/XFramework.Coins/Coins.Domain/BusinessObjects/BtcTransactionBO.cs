namespace Coins.Domain.BusinessObjects
{
    public class BtcTransactionBO
    {
        public long Id { get; set; }
        public string BtcAddress { get; set; }
        public decimal BtcAmount { get; set; }
    }
}