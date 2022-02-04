using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;

namespace IdentityServer.Api.SignalR.Handlers;

public class CheckCredentialExistenceHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<CheckCredentialExistenceRequest, CheckCredentialExistenceQuery>(connection, mediator);
    }
}