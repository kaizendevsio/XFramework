using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domin.Generic.Contracts.Requests.Create;

public class CreateDirectMessageRequest : RequestBase
{
    public string? Recipient { get; set; }   
    public Guid? MessageType { get; set; }   
    public string? Message { get; set; }   
}