using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Update;

public class UpdateMessageDeliveryRequest : RequestBase
{
    public Guid? MessageThreadMemberGuid { get; set; }
    public Guid? MessageGuid { get; set; }
    public Guid? EntityGuid { get; set; }
}