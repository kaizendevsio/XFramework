using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public class CreateMessageDeliveryEntityRequest : RequestBase
{
    public string? Name { get; set; }
}