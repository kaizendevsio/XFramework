namespace SmsGateway.Domain.Generic.Contracts.Responses.Sms;

public class MessageDirectResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long? ParentMessageId { get; set; }
    public long TypeId { get; set; }
    public long SenderId { get; set; }
    public long? RecipientId { get; set; }
    public string Sender { get; set; } = null!;
    public string Recipient { get; set; } = null!;
    public string Intent { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Guid { get; set; } = null!;
    public int Status { get; set; }
    
    public virtual MessageDirectResponse? ParentMessage { get; set; }

}