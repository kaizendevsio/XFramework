using SmsGateway.Core.DataAccess.Query.Entity.Sms;
using SmsGateway.Domain.Generic.Contracts.Requests.Get;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;

namespace SmsGateway.Api.SignalR.Handlers.Sms;

public class GetScheduledSmsMessageListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetScheduledSmsMessageListRequest, GetScheduledSmsMessageListQuery, List<MessageDirectResponse>>(connection, mediator);  
    }
}