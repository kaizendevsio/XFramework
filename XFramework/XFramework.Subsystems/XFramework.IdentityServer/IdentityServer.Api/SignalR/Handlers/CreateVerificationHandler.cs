using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Verification;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Verification;

namespace IdentityServer.Api.SignalR.Handlers;

public class CreateVerificationHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleVoidRequestCmd<CreateVerificationRequest, CreateVerificationCmd>(connection, mediator);
    }
}