namespace Messaging.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record ThreadListItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public Guid TypeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public bool IsMuted { get; set; }
    public bool IsArchived { get; set; }
}
