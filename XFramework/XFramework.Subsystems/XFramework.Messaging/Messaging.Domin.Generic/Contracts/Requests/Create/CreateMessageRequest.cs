using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domin.Generic.Contracts.Requests.Create;

public class CreateMessageRequest : RequestBase
{
    public Guid? ThreadGuid { get; set; }   
    public Guid? Message { get; set; }   
}