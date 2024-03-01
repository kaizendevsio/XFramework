using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Update;

public record UpdateMessageFileRequest : RequestBase
{
    public Guid? MessageGuid { get; set; }
    public Guid? StorageGuid { get; set; }
}