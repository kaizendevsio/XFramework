using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;

namespace IdentityServer.Api.SignalR.Handlers;

public class CheckIdentityExistenceHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<CheckIdentityExistenceRequest, CheckIdentityExistenceQuery>(connection, mediator);
    }
}