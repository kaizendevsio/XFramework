using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domin.Generic.Contracts.Requests.Create;

public class CreateDirectMessageRequest : RequestBase
{
    public Guid? MessageType { get; set; } 
    public string? Sender { get; set; }
    public string? Recipient { get; set; }
    public string? Subject { get; set; }
    public string? Intent { get; set; }
    public string? Message { get; set; }
    public DateTime? SendSchedule { get; set; }
    public bool IsScheduled { get; set; }
}