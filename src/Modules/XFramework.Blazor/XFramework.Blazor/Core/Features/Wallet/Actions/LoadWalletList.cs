using Wallets.Integration.Drivers;
using XFramework.Blazor.Entity.Models.Requests.Common;

namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState
{
    public record LoadWalletList : NavigableRequest, IAction;
    
    protected class LoadWalletListHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<LoadWalletList>(handlerServices, store)
    {
        private WalletState CurrentState => Store.GetState<WalletState>();
        
        public override async Task Handle(LoadWalletList action, CancellationToken aCancellationToken)
        {
            if (SessionState.State is not CurrentSessionState.Active) return;
            var response = await walletsServiceWrapper.Wallet.GetList(
                pageSize: 100,
                pageNumber: 1,
                filter: new()
                {
                    new()
                    {
                        PropertyName = nameof(Domain.Shared.Contracts.Wallet.CredentialId),
                        Operation = QueryFilterOperation.Equal,
                        Value = SessionState.Credential.Id
                    }
                },
                includeNavigations: true,
                includes: new List<string>
                {
                    $"{nameof(Domain.Shared.Contracts.Wallet.WalletType)}",
                });

            // Handle if the response is invalid or error
            if (await HandleFailure(response, action, true)) return;

            // Set Session State To Active
            CurrentState.WalletList = response.Response?.Items.ToList();

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, true);
        }
    }
}