using Wallets.Core.DataAccess.Query.Entity.Wallets;
using Wallets.Domain.Generic.Contracts.Requests.Get;

namespace Wallets.Api.SignalR.Handlers.Wallets.Entity;

public class GetWalletEntityListHandler  : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetWalletEntityListRequest, GetWalletEntityListQuery, List<WalletEntityResponse>>(connection, mediator);
    }
}