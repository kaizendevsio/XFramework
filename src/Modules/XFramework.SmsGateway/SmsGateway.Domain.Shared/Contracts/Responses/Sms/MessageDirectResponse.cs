using XFramework.Domain.Shared.Contracts.Base;

namespace SmsGateway.Domain.Shared.Contracts.Responses.Sms;

public class MessageDirectResponse : BaseModel
{
    public long? ParentMessageId { get; set; }
    public long TypeId { get; set; }
    public long SenderId { get; set; }
    public long? RecipientId { get; set; }
    public Guid AgentClusterId { get; set; }
    public string Sender { get; set; } = null!;
    public string Recipient { get; set; } = null!;
    public string Intent { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Guid { get; set; } = null!;
    public int Status { get; set; }
    
    public virtual MessageDirectResponse? ParentMessage { get; set; }

}