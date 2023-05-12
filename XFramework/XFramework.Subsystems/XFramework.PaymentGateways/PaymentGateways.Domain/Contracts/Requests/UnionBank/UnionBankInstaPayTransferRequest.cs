using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.Contracts.Requests.UnionBank;

public class UnionBankInstaPayTransferRequest
{
    [JsonPropertyName("senderRefId")]
    public string SenderRefId { get; set; }

    [JsonPropertyName("tranRequestDate")]
    public string TranRequestDate { get; set; }

    [JsonPropertyName("sender")]
    public UnionBankInstaPayTransferSender Sender { get; set; }

    [JsonPropertyName("beneficiary")]
    public UnionBankInstaPayTransferBeneficiary Beneficiary { get; set; }

    [JsonPropertyName("remittance")]
    public UnionBankInstaPayTransferRemittance Remittance { get; set; }
    
    public class UnionBankInstaPayTransferAddress
    {
        [JsonPropertyName("line1")]
        public string Line1 { get; set; }

        [JsonPropertyName("line2")]
        public string Line2 { get; set; }

        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonPropertyName("province")]
        public string Province { get; set; }

        [JsonPropertyName("zipCode")]
        public double ZipCode { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }
    }

    public class UnionBankInstaPayTransferBeneficiary
    {
        [JsonPropertyName("accountNumber")]
        public string AccountNumber { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public UnionBankInstaPayTransferAddress Address { get; set; }
    }

    public class UnionBankInstaPayTransferRemittance
    {
        [JsonPropertyName("amount")]
        public string Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("receivingBank")]
        public string ReceivingBank { get; set; }

        [JsonPropertyName("purpose")]
        public string Purpose { get; set; }

        [JsonPropertyName("instructions")]
        public string Instructions { get; set; }
    }

    public class UnionBankInstaPayTransferSender
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("address")]
        public UnionBankInstaPayTransferAddress Address { get; set; }
    }
}