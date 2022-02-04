using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;

namespace IdentityServer.Api.SignalR.Handlers;

public class CheckContactExistenceHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<CheckContactExistenceRequest, CheckContactExistenceQuery>(connection, mediator);
    }
}