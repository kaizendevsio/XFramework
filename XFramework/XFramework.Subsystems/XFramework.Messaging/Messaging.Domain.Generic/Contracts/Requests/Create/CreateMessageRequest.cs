using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public class CreateMessageRequest : RequestBase
{
    public string? Text { get; set; }
    public Guid? MessageThreadGuid { get; set; }
    public Guid? MessageThreadMemberGuid { get; set; }
}