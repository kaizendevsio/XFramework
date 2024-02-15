using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public record CreateMessageTypeRequest : RequestBase
{
    public short Priority { get; set; }
}