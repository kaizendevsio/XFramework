using Community.Core.DataAccess.Commands.Entity.Content;
using Community.Core.DataAccess.Query.Entity.Content;
using Community.Domain.Generic.Contracts.Requests.Delete;
using Community.Domain.Generic.Contracts.Requests.Get;
using Community.Domain.Generic.Contracts.Responses.Content;

namespace Community.Api.SignalR.Handlers.Content;

public class GetContentHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetContentRequest, GetContentQuery, CommunityContentResponse>(connection, mediator);
    }
}