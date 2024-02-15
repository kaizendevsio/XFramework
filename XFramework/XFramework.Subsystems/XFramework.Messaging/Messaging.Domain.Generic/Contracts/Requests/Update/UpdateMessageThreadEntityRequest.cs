using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Update;

public record UpdateMessageThreadEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public Guid? MessageTypeGuid { get; set; }
}