using XFramework.Domain.Generic.Contracts.Requests;

namespace Messaging.Domin.Generic.Contracts.Requests.Update;

public class ConfirmMessageSentRequest : RequestBase
{
    public Guid? Guid { get; set; }
}