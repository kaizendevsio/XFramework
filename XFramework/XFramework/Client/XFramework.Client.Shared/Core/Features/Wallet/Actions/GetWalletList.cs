using Wallets.Integration.Drivers;
using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class GetWalletList : NavigableRequest, IAction;
    
    protected class GetWalletListHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : ActionHandler<GetWalletList>(handlerServices, store)
    {
        public override async Task Handle(GetWalletList action, CancellationToken aCancellationToken)
        {
            if (SessionState.State is not CurrentSessionState.Active) return;
            var response = await walletsServiceWrapper.Wallet.GetList(pageSize: 10, pageNumber: 1, filter: new()
            {
                new()
                {
                    PropertyName = nameof(Domain.Generic.Contracts.Wallet.CredentialId),
                    Operation = QueryFilterOperation.Equal,
                    Value = SessionState.Credential.Id
                }
            });

            // Handle if the response is invalid or error
            if (await HandleFailure(response, action, true)) return;

            // Set Session State To Active
            await Mediator.Send(new SetState() { WalletList = response.Response.Items.ToList() });

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, true);
        }
    }
}