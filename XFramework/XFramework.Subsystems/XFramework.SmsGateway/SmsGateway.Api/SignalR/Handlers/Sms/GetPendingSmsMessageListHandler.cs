using SmsGateway.Core.DataAccess.Query.Entity.Sms;
using SmsGateway.Domain.Generic.Contracts.Requests.Get;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;

namespace SmsGateway.Api.SignalR.Handlers.Sms;

public class GetPendingSmsMessageListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetPendingSmsMessageListRequest, GetPendingSmsMessageListQuery, List<MessageDirectResponse>>(connection, mediator);
    }
}