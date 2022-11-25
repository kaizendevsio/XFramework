namespace Messaging.Domain.Generic.Contracts.Responses;

public class MessageThreadResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Guid { get; set; }

    public MessageThreadEntityResponse? Entity { get; set; }
}