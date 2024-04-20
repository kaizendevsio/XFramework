using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateMessageTypeRequest : RequestBase
{
    public short Priority { get; set; }
}