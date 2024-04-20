using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateMessageFileRequest : RequestBase
{
    public Guid? MessageGuid { get; set; }
    public Guid? StorageGuid { get; set; }
}