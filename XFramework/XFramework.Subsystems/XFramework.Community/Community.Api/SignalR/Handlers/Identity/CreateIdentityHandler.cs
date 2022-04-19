using Community.Core.DataAccess.Commands.Entity.Identity;
using Community.Domain.Generic.Contracts.Requests.Create;

namespace Community.Api.SignalR.Handlers.Identity;

public class CreateIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<CreateIdentityRequest, CreateIdentityCmd>(connection, mediator);
    }
}