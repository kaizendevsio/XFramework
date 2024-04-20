using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageRequest : RequestBase
{
    public string? Text { get; set; }
    public Guid? MessageThreadGuid { get; set; }
    public Guid? MessageThreadMemberGuid { get; set; }
}