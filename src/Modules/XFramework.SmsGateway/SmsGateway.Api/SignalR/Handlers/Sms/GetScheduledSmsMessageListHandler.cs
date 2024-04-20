using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Api.SignalR.Handlers.Sms;

public class GetScheduledSmsMessageListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory)
    {
        HandleRequestQuery<GetScheduledSmsMessageListRequest, List<MessageDirectResponse>>(connection, mediator, logger, scopeFactory); 
    }
}