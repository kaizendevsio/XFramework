namespace IdentityServer.Api.SignalR.Handlers;

public class GetRoleListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetRoleListRequest, GetRoleListQuery>(connection, mediator);
    }
}