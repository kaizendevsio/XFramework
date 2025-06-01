namespace Payments.Domain.Shared.Contracts;

[MemoryPackable]
public partial record TransactionRequestBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public string? MerchantId { get; set; }
    public MerchantCredentials? MerchantCredentials { get; set; }
}
