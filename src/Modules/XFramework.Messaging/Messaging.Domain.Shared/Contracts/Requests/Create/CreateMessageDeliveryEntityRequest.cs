using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateMessageDeliveryEntityRequest : RequestBase
{
    public string? Name { get; set; }
}