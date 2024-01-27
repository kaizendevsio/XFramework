using Wallets.Integration.Drivers;
using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public record GetWallet(Guid Id) : NavigableRequest;
    
    protected class GetWalletHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<GetWallet>(handlerServices, store)
    {

        private WalletState CurrentState => Store.GetState<WalletState>();
        public override async Task Handle(GetWallet action, CancellationToken aCancellationToken)
        {
            if (SessionState.State is not CurrentSessionState.Active) return;
            var response = await walletsServiceWrapper.Wallet.Get(action.Id);

            // Handle if the response is invalid or error
            if (await HandleFailure(response, action, true)) return;

            // Set Session State To Active
            CurrentState.Selected = response.Response;

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, true);

            return;
        }
    }
}