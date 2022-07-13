using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Delete.Address;

namespace IdentityServer.Api.SignalR.Handlers;

public class DeleteAddressHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteAddressRequest, DeleteAddressCmd>(connection, mediator);
    }
}