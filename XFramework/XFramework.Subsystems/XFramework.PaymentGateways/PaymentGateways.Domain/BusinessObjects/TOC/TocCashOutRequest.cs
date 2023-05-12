using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.BusinessObjects.TOC;

public class TocCashOutRequest
{
    [JsonPropertyName("acctName")]
    public string AcctName { get; set; }

    [JsonPropertyName("acctNo")]
    public string AcctNo { get; set; }

    [JsonPropertyName("bankCode")]
    public string BankCode { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("txnAmt")]
    public string TxnAmt { get; set; }
}