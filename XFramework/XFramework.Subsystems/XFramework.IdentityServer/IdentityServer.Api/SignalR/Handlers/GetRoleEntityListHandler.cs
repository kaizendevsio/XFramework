namespace IdentityServer.Api.SignalR.Handlers;

public class GetRoleEntityListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetRoleEntityListRequest, GetRoleEntityListQuery>(connection, mediator);
    }
}