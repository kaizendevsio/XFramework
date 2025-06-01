namespace Payments.Domain.Shared.Contracts;

[MemoryPackable]
public partial record PaymentResponse
{
    public bool Success { get; set; }
    public string? ReferenceId { get; set; }
    public string? Message { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public DateTimeOffset TransactionDate { get; set; } = DateTimeOffset.UtcNow;
    public string? ProviderResponseCode { get; set; }
    public string? ProviderResponseMessage { get; set; }
    public string? ProviderResponse { get; set; }
}
