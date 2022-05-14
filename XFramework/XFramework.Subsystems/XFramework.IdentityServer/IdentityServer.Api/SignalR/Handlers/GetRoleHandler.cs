namespace IdentityServer.Api.SignalR.Handlers;

public class GetRoleHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetRoleRequest, GetRoleQuery, RoleResponse>(connection, mediator);
    }
}