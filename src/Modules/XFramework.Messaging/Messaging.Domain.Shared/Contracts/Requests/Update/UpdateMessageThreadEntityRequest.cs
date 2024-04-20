using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageThreadEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public Guid? MessageTypeGuid { get; set; }
}