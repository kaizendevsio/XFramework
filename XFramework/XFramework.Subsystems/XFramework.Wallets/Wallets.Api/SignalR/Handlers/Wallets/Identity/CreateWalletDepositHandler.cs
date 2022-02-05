using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;
using Wallets.Domain.Generic.Contracts.Requests.Create;

namespace Wallets.Api.SignalR.Handlers.Wallets.Identity;

public class CreateWalletDepositHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<CreateWalletDepositRequest, CreateWalletDepositQuery>(connection, mediator);
    }
}