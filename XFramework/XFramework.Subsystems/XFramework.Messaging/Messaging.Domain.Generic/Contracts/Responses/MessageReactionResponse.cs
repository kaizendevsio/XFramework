namespace Messaging.Domain.Generic.Contracts.Responses;

public class MessageReactionResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }

    public MessageReactionEntityResponse? Entity { get; set; }
    public MessageResponse? Message { get; set; }
}