namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record GetMessageReceiptsResponse
{
    public List<MessageReceiptItemResponse> Items { get; set; } = [];
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    /// <summary>False when the conversation or the tenant has read receipts switched off. Every
    /// <see cref="MessageReceiptItemResponse.ReadAt"/> is then null, and the caller shows no read
    /// section at all rather than an empty one that looks like "nobody has read this".</summary>
    public bool ReadReceiptsEnabled { get; set; }
}

/// <summary>One member's receipt. A member with no row at all is simply absent: the caller already
/// holds the authorized roster, so "not delivered yet" costs no extra read.</summary>
[MemoryPackable]
public partial record MessageReceiptItemResponse
{
    public Guid MemberId { get; set; }
    public Guid CredentialId { get; set; }
    public DateTime DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
