using System;
using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.Contracts.Responses.TOC;

public class TocCashInResponse
{
    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("result")]
    public ResultObject Result { get; set; }
    
    public class ResultObject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("txId")]
        public string TxId { get; set; }

        [JsonPropertyName("instrId")]
        public string InstrId { get; set; }

        [JsonPropertyName("msgId")]
        public string MsgId { get; set; }

        [JsonPropertyName("creDtTm")]
        public DateTime CreDtTm { get; set; }

        [JsonPropertyName("ttlIntrBkSttlmAmt")]
        public string TtlIntrBkSttlmAmt { get; set; }

        [JsonPropertyName("endToEndId")]
        public string EndToEndId { get; set; }

        [JsonPropertyName("cdtrAgtBICFI")]
        public string CdtrAgtBICFI { get; set; }

        [JsonPropertyName("trxStatus")]
        public string TrxStatus { get; set; }
    }
}