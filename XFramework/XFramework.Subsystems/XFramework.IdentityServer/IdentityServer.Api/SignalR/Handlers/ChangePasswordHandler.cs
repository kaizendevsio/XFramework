using IdentityServer.Domain.Generic.Contracts.Requests.Update;

namespace IdentityServer.Api.SignalR.Handlers;

public class ChangePasswordHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<UpdatePasswordRequest, ChangePasswordCmd>(connection, mediator);
    }
}