using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;

namespace IdentityServer.Api.SignalR.Handlers;

public class CreateAddressHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateAddressRequest, CreateAddressCmd>(connection, mediator);
    }
}