namespace Wallets.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record WalletEventResponse
{
    public Guid EventId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid WalletId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid TenantId { get; set; }
    public decimal? Amount { get; set; }
    public string? TransactionType { get; set; }
    public string? ReferenceNumber { get; set; }
    public decimal? RunningBalance { get; set; }
    public string? Reason { get; set; }
    public decimal? Threshold { get; set; }
    public Guid? OriginalTransactionId { get; set; }
    public Guid? ReversalTransactionId { get; set; }
}
