using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Domain.Generic.Contracts.Requests.Delete;

namespace Wallets.Api.SignalR.Handlers.Wallets.Identity;

public class DeleteWalletHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteWalletRequest, DeleteWalletCmd>(connection, mediator);
    }
}