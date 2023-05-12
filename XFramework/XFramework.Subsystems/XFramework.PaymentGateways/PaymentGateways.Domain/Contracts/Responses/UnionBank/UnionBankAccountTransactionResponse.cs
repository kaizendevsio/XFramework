using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.Contracts.Responses.UnionBank;

public class UnionBankAccountTransactionResponse
{
    [JsonPropertyName("records")]
    public List<UnionBankAccountTransactionRecord> Records { get; set; }

    [JsonPropertyName("totalRecords")]
    public string TotalRecords { get; set; }

    [JsonPropertyName("lastRunningBalance")]
    public string LastRunningBalance { get; set; }
    
    public class UnionBankAccountTransactionRecord
    {
        [JsonPropertyName("recordNumber")]
        public string RecordNumber { get; set; }

        [JsonPropertyName("tranId")]
        public string TranId { get; set; }

        [JsonPropertyName("tranType")]
        public string TranType { get; set; }

        [JsonPropertyName("amount")]
        public string Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("tranDate")]
        public DateTime TranDate { get; set; }

        [JsonPropertyName("remarks2")]
        public string Remarks2 { get; set; }

        [JsonPropertyName("remarks")]
        public string Remarks { get; set; }

        [JsonPropertyName("balance")]
        public string Balance { get; set; }

        [JsonPropertyName("balanceCurrency")]
        public string BalanceCurrency { get; set; }

        [JsonPropertyName("postedDate")]
        public DateTime PostedDate { get; set; }

        [JsonPropertyName("tranDescription")]
        public string TranDescription { get; set; }
    }
}