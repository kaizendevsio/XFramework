using XFramework.Domain.Generic.Contracts.Requests;

namespace SmsGateway.Domain.Generic.Contracts.Requests.Update;

public class ConfirmSmsMessageSentRequest : RequestBase
{
    public Guid? Guid { get; set; }
}