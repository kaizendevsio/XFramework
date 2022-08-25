using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Create;

public class CreateMessageFileRequest : RequestBase
{
    public Guid? MessageGuid { get; set; }
    public Guid? StorageGuid { get; set; }
}