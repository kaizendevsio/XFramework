namespace Messaging.Domain.Shared.Contracts.Responses;

public record MessageDeliveryResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Guid { get; set; }

    public MessageThreadMemberResponse? ThreadMember { get; set; }
    public MessageResponse? Message { get; set; }
    public MessageDeliveryEntityResponse? Entity { get; set; }
}