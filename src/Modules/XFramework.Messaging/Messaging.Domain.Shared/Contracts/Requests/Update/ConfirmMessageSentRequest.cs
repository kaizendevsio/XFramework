using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

public record ConfirmMessageSentRequest : RequestBase
{
    public Guid? Guid { get; set; }
}