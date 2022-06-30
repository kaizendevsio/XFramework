using IdentityServer.Core.DataAccess.Query.Entity.Identity;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetIdentityRequest, GetIdentityQuery, IdentityResponse>(connection, mediator);
    }
}