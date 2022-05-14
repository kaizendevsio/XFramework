using IdentityServer.Core.DataAccess.Query.Entity.Identity;

namespace IdentityServer.Api.SignalR.Handlers;

public class CheckIdentityExistenceHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<CheckIdentityExistenceRequest, CheckIdentityExistenceQuery, ExistenceResponse>(connection, mediator);
    }
}