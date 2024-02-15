using SmsGateway.Domain.Generic.Contracts.Requests.Get;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;

namespace SmsGateway.Api.SignalR.Handlers.Sms;

public class GetPendingSmsMessageListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory)
    {
        HandleRequestQuery<GetPendingSmsMessageListRequest, List<MessageDirectResponse>>(connection, mediator, logger, scopeFactory);
    }
}