using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateMessageThreadEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public Guid? MessageTypeGuid { get; set; }
}