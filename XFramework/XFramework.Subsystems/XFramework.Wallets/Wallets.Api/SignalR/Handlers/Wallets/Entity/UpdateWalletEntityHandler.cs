using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Domain.Generic.Contracts.Requests.Update;

namespace Wallets.Api.SignalR.Handlers.Wallets.Entity;

public class UpdateWalletEntityHandler  : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateWalletEntityRequest, UpdateWalletEntityCmd>(connection, mediator);
    }
}