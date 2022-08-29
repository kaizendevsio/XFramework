using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Requests.Get;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Api.SignalR.Handlers.Message.Get;

public class GetMessageDeliveryHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetMessageDeliveryRequest, GetMessageDeliveryQuery, MessageDeliveryResponse>(connection, mediator);
    }
}