namespace Messaging.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record ThreadMessageItemResponse
{
    public Guid Id { get; set; }
    public string Text { get; set; } = null!;
    public Guid SenderCredentialId { get; set; }
    public string SenderAlias { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public Guid? ParentMessageId { get; set; }
    public List<Guid> MentionedCredentialIds { get; set; } = [];
    public bool IsPinned { get; set; }
    public bool IsSaved { get; set; }
}
