using Wallets.Integration.Drivers;
using XFramework.Blazor.Entity.Models.Requests.Common;

namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState
{
    public record GetWallet(Guid Id) : NavigableRequest<Domain.Shared.Contracts.Wallet?>;
    
    protected class GetWalletHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<GetWallet, Domain.Shared.Contracts.Wallet?>(handlerServices, store)
    {

        private WalletState CurrentState => Store.GetState<WalletState>();
        public override async Task<Domain.Shared.Contracts.Wallet?> Handle(GetWallet action, CancellationToken aCancellationToken)
        {
            if (SessionState.State is not CurrentSessionState.Active) return null;
            var response = await walletsServiceWrapper.Wallet.Get(action.Id);

            // Handle if the response is invalid or error
            if (await HandleFailure(response, action, true)) return null;
            
            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, true);

            return response.Response;
        }
    }
}