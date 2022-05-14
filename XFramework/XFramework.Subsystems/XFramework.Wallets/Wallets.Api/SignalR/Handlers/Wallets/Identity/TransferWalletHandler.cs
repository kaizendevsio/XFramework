using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Domain.Generic.Contracts.Requests.Update;

namespace Wallets.Api.SignalR.Handlers.Wallets.Identity;

public class TransferWalletHandler  : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<TransferWalletRequest, TransferWalletCmd>(connection, mediator);
    }
}