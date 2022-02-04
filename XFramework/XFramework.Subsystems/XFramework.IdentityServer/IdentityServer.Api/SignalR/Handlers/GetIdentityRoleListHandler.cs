namespace IdentityServer.Api.SignalR.Handlers;

public class GetIdentityRoleListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetIdentityRoleListRequest, GetIdentityRoleListQuery>(connection, mediator);
    }
}