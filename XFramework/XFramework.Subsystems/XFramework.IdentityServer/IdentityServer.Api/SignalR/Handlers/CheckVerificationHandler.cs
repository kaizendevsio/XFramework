using IdentityServer.Core.DataAccess.Query.Entity.Identity.Verifications;
using IdentityServer.Domain.Generic.Contracts.Requests.Check.Verification;

namespace IdentityServer.Api.SignalR.Handlers;

public class CheckVerificationHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<CheckVerificationRequest, CheckVerificationQuery>(connection, mediator);
    }
}