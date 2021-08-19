namespace PaymentGateways.Domain.Generic.Contracts.Requests
{
    public class DepositRequest
    {
        public long? TargetWalletTypeId { get; set; }
        public decimal? Amount { get; set; }
        public string Remarks { get; set; }
    }
}