using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public class CreateMessageThreadEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public Guid? MessageTypeGuid { get; set; }
}