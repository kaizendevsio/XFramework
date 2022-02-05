using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Wallets.Core.DataAccess.Query.Entity.Wallets;
using Wallets.Domain.Generic.Contracts.Requests.Get;

namespace Wallets.Api.SignalR.Handlers.Wallets.Entity;

public class GetWalletEntityListHandler  : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<GetWalletEntityListRequest, GetWalletEntityListQuery>(connection, mediator);
    }
}