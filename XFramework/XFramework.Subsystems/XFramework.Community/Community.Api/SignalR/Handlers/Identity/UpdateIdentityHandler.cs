using Community.Core.DataAccess.Commands.Entity.Identity;
using Community.Domain.Generic.Contracts.Requests.Update;

namespace Community.Api.SignalR.Handlers.Identity;

public class UpdateIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleVoidRequestCmd<UpdateCommunityIdentityRequest, UpdateCommunityIdentityCmd>(connection, mediator);
    }
}