namespace Messaging.Domain.Shared.Contracts.Responses;

public record MessageResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Text { get; set; }
    public string? Guid { get; set; }

    public MessageThreadResponse? Thread { get; set; }
    public MessageThreadMemberResponse? ThreadMember { get; set; }
}