using SmsGateway.Domain.Generic.Contracts.Requests.Create;

namespace SmsGateway.Api.SignalR.Handlers.Sms;

public class CreateSmsMessageHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory)
    {
        HandleRequestCmd<CreateSmsMessageRequest>(connection, mediator, logger, scopeFactory);
    }
}