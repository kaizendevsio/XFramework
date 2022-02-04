using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetCredentialHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetCredentialRequest, GetCredentialQuery>(connection, mediator);
    }
}