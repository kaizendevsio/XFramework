using SmsGateway.Core.DataAccess.Query.Entity.Sms;
using SmsGateway.Domain.Generic.Contracts.Requests.Get;

namespace SmsGateway.Api.SignalR.Handlers.Sms;

public class GetScheduledSmsMessageListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetScheduledSmsMessageListRequest, GetScheduledSmsMessageListQuery>(connection, mediator);
    }
}