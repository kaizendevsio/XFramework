namespace Payments.Domain.Shared.Contracts;

[MemoryPackable]
public partial record PaymentCallbackPayload
{
    public string? ReferenceNumber { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? MerchantId { get; set; }
    public decimal? Amount { get; set; }
    public string? Status { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PayerName { get; set; }
    public string? PayerAccount { get; set; }
    public DateTimeOffset? TransactionDate { get; set; }
    public Dictionary<string, string> RawParameters { get; set; } = new();
    public string? RawBody { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? SignatureOrHash { get; set; }
}
