using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Domain.Generic.Contracts.Requests.Update;

namespace Wallets.Api.SignalR.Handlers.Wallets.Entity;

public class UpdateWalletEntityHandler  : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<UpdateWalletEntityRequest, UpdateWalletEntityCmd>(connection, mediator);
    }
}