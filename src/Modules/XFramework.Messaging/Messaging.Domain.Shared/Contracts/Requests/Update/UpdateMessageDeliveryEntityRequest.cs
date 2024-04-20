using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageDeliveryEntityRequest : RequestBase
{
    public string? Name { get; set; }
}