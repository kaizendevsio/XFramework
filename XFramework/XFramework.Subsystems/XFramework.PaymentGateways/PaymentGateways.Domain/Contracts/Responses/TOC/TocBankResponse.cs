using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.Contracts.Responses.TOC;

public class TocBankResponse
{
    [JsonPropertyName("bankID")]
    public string BankID { get; set; }

    [JsonPropertyName("bankName")]
    public string BankName { get; set; }

    [JsonPropertyName("bankCode")]
    public string BankCode { get; set; }

    [JsonPropertyName("bankNumCode")]
    public string BankNumCode { get; set; }

    [JsonPropertyName("isBank")]
    public bool IsBank { get; set; }

    [JsonPropertyName("isQr")]
    public bool IsQr { get; set; }

    [JsonPropertyName("bankALength")]
    public string BankALength { get; set; }

    [JsonPropertyName("isMaintenance")]
    public bool IsMaintenance { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("logo")]
    public string Logo { get; set; }
}