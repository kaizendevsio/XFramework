using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domain.Generic.Contracts.Requests.Update;

public record ConfirmMessageSentRequest : RequestBase
{
    public Guid? Guid { get; set; }
}