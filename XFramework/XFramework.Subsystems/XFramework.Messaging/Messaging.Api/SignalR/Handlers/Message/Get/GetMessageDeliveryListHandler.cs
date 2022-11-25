using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Requests.Get;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Api.SignalR.Handlers.Message.Get;

public class GetMessageDeliveryListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetMessageDeliveryListRequest, GetMessageDeliveryListQuery, List<MessageDeliveryResponse>>(connection, mediator);
    }
}