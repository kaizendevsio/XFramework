using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageTypeRequest : RequestBase
{
    public short Priority { get; set; }
}