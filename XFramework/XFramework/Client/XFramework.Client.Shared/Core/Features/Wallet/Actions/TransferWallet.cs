﻿using Wallets.Integration.Drivers;
using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public record TransferWallet : NavigableRequest, IAction
    {
        public TransactionPurpose TransactionPurpose { get; set; }
    }
    
    protected class TransferWalletHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<TransferWallet>(handlerServices, store)
    {
        public WalletState CurrentState => Store.GetState<WalletState>();

        public override async Task Handle(TransferWallet action, CancellationToken aCancellationToken)
        {
            var result = await walletsServiceWrapper.TransferWallet(new()
            {
                ReferenceNumber = CurrentState.SendWalletVm.ClientReference,
                CredentialId = CurrentState.SendWalletVm.SenderCredentialId,
                WalletTypeId = CurrentState.SendWalletVm.WalletTypeId,
                RecipientCredentialId = CurrentState.SendWalletVm.RecipientCredentialId,
                Amount = CurrentState.SendWalletVm.Amount,
                Remarks = CurrentState.SendWalletVm.Remarks,
                OnHold = CurrentState.SendWalletVm.OnHold,
                CurrencyId = new Guid("7ee3621a-5878-4c16-8112-eab11f29db95")
            });

            if (result.HttpStatusCode is HttpStatusCode.Accepted)
            {
                Mediator.Send(new GetWalletList());
            }

            if (await HandleFailure(result, action, silent: action.Silent))
            {
                WalletState.SendWalletVm.OnFailure?.Invoke();
                return;
            }
            
            WalletState.SendWalletVm.OnSuccess?.Invoke();
            await HandleSuccess(result, action, silent: action.Silent);
        }
    }
}