using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;
using Wallets.Domain.Generic.Contracts.Requests.Get;

namespace Wallets.Api.SignalR.Handlers.Wallets.Identity;

public class GetWalletHandler  : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetWalletRequest, GetWalletQuery, WalletResponse>(connection, mediator);
    }
}