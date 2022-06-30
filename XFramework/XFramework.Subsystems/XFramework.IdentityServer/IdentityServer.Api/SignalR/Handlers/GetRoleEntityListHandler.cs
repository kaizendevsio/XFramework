namespace IdentityServer.Api.SignalR.Handlers;

public class GetRoleEntityListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetRoleEntityListRequest, GetRoleEntityListQuery, List<RoleEntityResponse>>(connection, mediator);
    }
}