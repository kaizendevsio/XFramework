using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Update;

public class UpdateMessageTypeRequest : RequestBase
{
    public short Priority { get; set; }
}