using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageReactionRequest : RequestBase
{
    public Guid? MessageGuid { get; set; }
    public Guid? EntityGuid { get; set; }
}