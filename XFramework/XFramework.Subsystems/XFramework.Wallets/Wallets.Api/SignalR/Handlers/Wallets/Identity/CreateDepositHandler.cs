using PaymentGateways.Domain.Generic.Contracts.Requests.Create;
using PaymentGateways.Domain.Generic.Contracts.Responses;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;
using Wallets.Domain.Generic.Contracts.Requests.Create;

namespace Wallets.Api.SignalR.Handlers.Wallets.Identity;

public class CreateDepositHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateDepositRequest, CreateWalletDepositCmd, DepositResponse>(connection, mediator);
    }
}