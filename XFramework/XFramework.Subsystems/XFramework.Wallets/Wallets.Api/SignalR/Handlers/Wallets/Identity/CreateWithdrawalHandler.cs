using PaymentGateways.Domain.Generic.Contracts.Requests.Create;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;
using Wallets.Domain.Generic.Contracts.Requests.Create;

namespace Wallets.Api.SignalR.Handlers.Wallets.Identity;

public class CreateWithdrawalHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleVoidRequestCmd<CreateWithdrawalRequest, CreateWalletWithdrawalCmd>(connection, mediator);
    }
}