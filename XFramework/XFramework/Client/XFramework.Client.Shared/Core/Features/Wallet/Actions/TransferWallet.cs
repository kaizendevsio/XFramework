using Wallets.Integration.Drivers;
using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class TransferWallet : NavigableRequest, IAction
    {
        public TransactionPurpose TransactionPurpose { get; set; }
    }
    
    protected class TransferWalletHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : ActionHandler<TransferWallet>(handlerServices, store)
    {
        public WalletState CurrentState => Store.GetState<WalletState>();

        public override async Task Handle(TransferWallet action, CancellationToken aCancellationToken)
        {
            var result = await walletsServiceWrapper.TransferWallet(new()
            {
                ClientReference = CurrentState.SendWalletVm.ClientReference,
                CredentialId = CurrentState.SendWalletVm.SenderCredentialId,
                WalletTypeId = CurrentState.SendWalletVm.WalletTypeId,
                RecipientCredentialId = CurrentState.SendWalletVm.RecipientCredentialId,
                Amount = CurrentState.SendWalletVm.Amount,
                Remarks = CurrentState.SendWalletVm.Remarks,
            });

            if (result.HttpStatusCode is HttpStatusCode.Accepted)
            {
                Mediator.Send(new GetWalletList());
            }

            if (await HandleFailure(result, action, true)) return;
            await HandleSuccess(result, action, false, $"{(action.TransactionPurpose is TransactionPurpose.Payment ? "Payment" : "Transfer")} successful");
        }
    }
}