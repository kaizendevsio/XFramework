using Messaging.Core.DataAccess.Query.Entity.Message;
using Messaging.Domain.Generic.Contracts.Requests.Get;
using Messaging.Domain.Generic.Contracts.Responses;

namespace Messaging.Api.SignalR.Handlers.Message.Get;

public class GetMessageReactionEntityListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetMessageReactionEntityListRequest, GetMessageReactionEntityListQuery, List<MessageReactionEntityResponse>>(connection, mediator);
    }
}