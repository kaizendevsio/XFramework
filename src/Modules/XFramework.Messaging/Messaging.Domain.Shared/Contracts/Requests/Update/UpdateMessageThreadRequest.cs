using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageThreadRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? EntityGuid { get; set; }
}