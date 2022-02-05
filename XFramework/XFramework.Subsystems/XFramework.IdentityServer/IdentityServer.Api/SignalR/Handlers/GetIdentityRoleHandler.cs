namespace IdentityServer.Api.SignalR.Handlers;

public class GetIdentityRoleHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetIdentityRoleRequest, GetIdentityRoleQuery>(connection, mediator);
    }
}