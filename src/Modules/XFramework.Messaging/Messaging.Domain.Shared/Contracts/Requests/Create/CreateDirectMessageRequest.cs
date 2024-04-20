using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateDirectMessageRequest : RequestBase
{
    public Guid? MessageType { get; set; } 
    public string Sender { get; set; }
    public string Recipient { get; set; }
    public string Subject { get; set; }
    public string Intent { get; set; }
    public string Message { get; set; }
    public Guid? ParentMessageGuid { get; set; }
    public DateTime? SendSchedule { get; set; }
    public bool IsScheduled { get; set; }
}