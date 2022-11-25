using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public class CreateMessageTypeRequest : RequestBase
{
    public short Priority { get; set; }
}