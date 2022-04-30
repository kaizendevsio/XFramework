using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Verification;
using IdentityServer.Domain.Generic.Contracts.Requests.Update;
using IdentityServer.Domain.Generic.Contracts.Requests.Update.Verification;

namespace IdentityServer.Api.SignalR.Handlers;

public class UpdateVerificationHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<UpdateVerificationRequest, UpdateVerificationCmd>(connection, mediator);
    }
}