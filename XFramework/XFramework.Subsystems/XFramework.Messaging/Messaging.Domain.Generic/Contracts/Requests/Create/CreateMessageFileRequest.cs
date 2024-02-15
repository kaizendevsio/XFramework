using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public record CreateMessageFileRequest : RequestBase
{
    public Guid? MessageGuid { get; set; }
    public Guid? StorageGuid { get; set; }
}