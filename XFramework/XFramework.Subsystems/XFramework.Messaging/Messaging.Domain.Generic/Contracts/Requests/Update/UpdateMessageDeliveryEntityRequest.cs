using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Update;

public class UpdateMessageDeliveryEntityRequest : RequestBase
{
    public string? Name { get; set; }
}