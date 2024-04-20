using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record UpdateMessageFileRequest : RequestBase
{
    public Guid? MessageGuid { get; set; }
    public Guid? StorageGuid { get; set; }
}