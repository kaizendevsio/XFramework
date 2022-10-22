namespace Messaging.Domain.Generic.Contracts.Responses;

public class MessageDirectResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Sender { get; set; }
    public string? Recipient { get; set; }
    public string? Intent { get; set; }
    public string? Subject { get; set; }
    public string? Message { get; set; }
    public string? Guid { get; set; }
    public short Status { get; set; }

    public MessageDirectResponse? ParentMessage { get; set; }
    public MessageTypeResponse? Type { get; set; }
    /*public SenderResponse? Sender { get; set; }
    public RecipientResponse? Recipient { get; set; }*/
    
}