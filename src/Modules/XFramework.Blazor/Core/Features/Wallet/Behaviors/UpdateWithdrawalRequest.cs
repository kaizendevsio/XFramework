using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Abstractions;

namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState
{
    public record UpdateWithdrawalRequest : StateAction
    {
        public required WithdrawalRequest Context { get; set; }
    }

    protected class UpdateWithdrawalRequestHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        IHelperService helperService,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<UpdateWithdrawalRequest>(handlerServices, store)
    {
        public override async Task Handle(UpdateWithdrawalRequest action, CancellationToken aCancellationToken)
        {
            ReportBusy("Updating withdrawal request");
            
            var result = await walletsServiceWrapper.WithdrawalRequest.Patch(action.Context);

            if (await HandleFailure(result, action)) return;
            
            await HandleSuccess(action,"Withdrawal request updated successfully", silent: false);
        }
    }
}