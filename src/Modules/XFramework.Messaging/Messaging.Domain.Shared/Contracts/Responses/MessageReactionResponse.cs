namespace Messaging.Domain.Shared.Contracts.Responses;

public record MessageReactionResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }

    public MessageReactionEntityResponse? Entity { get; set; }
    public MessageResponse? Message { get; set; }
}