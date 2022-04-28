using SmsGateway.Core.DataAccess.Commands.Entity.Sms;
using SmsGateway.Domain.Generic.Contracts.Requests.Create;

namespace SmsGateway.Api.SignalR.Handlers.Sms;

public class CreateSmsMessageHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<CreateSmsMessageRequest, CreateSmsMessageCmd>(connection, mediator);
    }
}