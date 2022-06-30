using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Verification;
using IdentityServer.Core.DataAccess.Commands.Entity.Subscription;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Subscription;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Verification;

namespace IdentityServer.Api.SignalR.Handlers;

public class CreateAffiliateSubscriptionHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleVoidRequestCmd<CreateAffiliateSubscriptionRequest, CreateAffiliateSubscriptionCmd>(connection, mediator);
    }
}