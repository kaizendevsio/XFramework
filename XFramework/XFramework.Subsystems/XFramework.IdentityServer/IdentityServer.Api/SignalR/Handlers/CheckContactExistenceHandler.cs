using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;

namespace IdentityServer.Api.SignalR.Handlers;

public class CheckContactExistenceHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<CheckContactExistenceRequest, CheckContactExistenceQuery, ExistenceResponse>(connection, mediator);
    }
}