using IdentityServer.Core.DataAccess.Query.Entity.Identity.Verifications;
using IdentityServer.Domain.Generic.Contracts.Requests.Check.Verification;
using IdentityServer.Domain.Generic.Contracts.Responses.Verification;

namespace IdentityServer.Api.SignalR.Handlers;

public class CheckVerificationHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<CheckVerificationRequest, CheckVerificationQuery, IdentityVerificationSummaryResponse>(connection, mediator);
    }
}