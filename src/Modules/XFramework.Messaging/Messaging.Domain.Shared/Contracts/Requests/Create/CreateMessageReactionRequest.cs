using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateMessageReactionRequest : RequestBase
{
    public Guid? MessageGuid { get; set; }
    public Guid? EntityGuid { get; set; }
}