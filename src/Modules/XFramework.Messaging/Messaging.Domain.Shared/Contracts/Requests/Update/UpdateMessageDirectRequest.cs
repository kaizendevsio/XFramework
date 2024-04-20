using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageDirectRequest : RequestBase
{
    public Guid? ParentMessageGuid { get; set; }
    public Guid? TypeGuid { get; set; }
    public Guid? SenderGuid { get; set; }
    public Guid? RecipientGuid { get; set; }
    public string? Sender { get; set; }
    public string? Recipient { get; set; }
    public string? Intent { get; set; }
    public string? Subject { get; set; }
    public string? Message { get; set; }
    public short Status { get; set; }
}