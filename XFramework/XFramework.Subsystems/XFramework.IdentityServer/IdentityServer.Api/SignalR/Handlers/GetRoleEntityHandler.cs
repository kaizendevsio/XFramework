namespace IdentityServer.Api.SignalR.Handlers;

public class GetRoleEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetRoleEntityRequest, GetRoleEntityQuery, RoleEntityResponse>(connection, mediator);
    }
}