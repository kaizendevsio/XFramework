using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public record CreateMessageDeliveryRequest : RequestBase
{
    public Guid? MessageThreadMemberGuid { get; set; }
    public Guid? MessageGuid { get; set; }
    public Guid? EntityGuid { get; set; }
}