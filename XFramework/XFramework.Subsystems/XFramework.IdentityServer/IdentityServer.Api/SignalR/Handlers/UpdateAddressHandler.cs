using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Update.Address;

namespace IdentityServer.Api.SignalR.Handlers;

public class UpdateAddressHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateAddressRequest, UpdateAddressCmd>(connection, mediator);
    }
}