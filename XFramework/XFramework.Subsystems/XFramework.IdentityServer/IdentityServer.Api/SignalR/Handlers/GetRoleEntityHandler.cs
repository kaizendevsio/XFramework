namespace IdentityServer.Api.SignalR.Handlers;

public class GetRoleEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetRoleEntityRequest, GetRoleEntityQuery>(connection, mediator);
    }
}