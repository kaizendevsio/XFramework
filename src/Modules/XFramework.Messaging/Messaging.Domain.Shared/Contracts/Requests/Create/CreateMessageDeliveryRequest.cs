using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateMessageDeliveryRequest : RequestBase
{
    public Guid? MessageThreadMemberGuid { get; set; }
    public Guid? MessageGuid { get; set; }
    public Guid? EntityGuid { get; set; }
}