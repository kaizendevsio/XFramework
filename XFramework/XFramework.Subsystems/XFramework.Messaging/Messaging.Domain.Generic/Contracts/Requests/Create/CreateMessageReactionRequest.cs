using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public record CreateMessageReactionRequest : RequestBase
{
    public Guid? MessageGuid { get; set; }
    public Guid? EntityGuid { get; set; }
}