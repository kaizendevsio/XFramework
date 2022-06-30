using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Domain.Generic.Contracts.Requests.Delete;

namespace Wallets.Api.SignalR.Handlers.Wallets.Entity;

public class DeleteWalletEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteWalletEntityRequest, DeleteWalletEntityCmd>(connection, mediator);
    }
}