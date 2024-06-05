using Wallets.Integration.Drivers;
using XFramework.Blazor.Entity.Models.Requests.Common;
using XFramework.Blazor.Entity.Models.Requests.Wallet;
using XFramework.Blazor.Entity.Validations.Wallet;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState
{
    public record TransferWallet : StateAction<CmdResponse>
    {
        public Guid SenderCredentialId { get; set; }
        public Guid RecipientCredentialId { get; set; }
        public Guid WalletTypeId { get; set; }
        public decimal NetAmount => Amount - TotalFee;
        public decimal Amount => LineItems.Sum(x => x.Amount ?? 0);
        public bool OnHold { get; set; }
        public decimal TotalFee => LineItems.Sum(x => x.Fee) + Fee;
        public decimal Fee { get; set; }
        public string? Remarks { get; set; }
        public string? ClientReference { get; set; }
        public List<WalletTransactionLineItem> LineItems { get; set; } = [];
        public TransactionPurpose TransactionPurpose { get; set; } = TransactionPurpose.Transfer;
    }
    
    protected class TransferWalletHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<TransferWallet, CmdResponse>(handlerServices, store)
    {
        public WalletState CurrentState => Store.GetState<WalletState>();

        public override async Task<CmdResponse> Handle(TransferWallet action, CancellationToken aCancellationToken)
        {
            var result = await walletsServiceWrapper.TransferWallet(new()
            {
                ReferenceNumber = action.ClientReference,
                CredentialId = action.SenderCredentialId,
                WalletTypeId = action.WalletTypeId,
                RecipientCredentialId = action.RecipientCredentialId,
                LineItems = action.LineItems,
                Fee = action.Fee,
                Remarks = action.Remarks,
                OnHold = action.OnHold,
                CurrencyId = Constants.Currency.Php,
                TransactionPurpose = action.TransactionPurpose
            });

            if (await HandleFailure(result, action)) return result;

            Mediator.Send(new LoadWalletList());

            await HandleSuccess(result, action);
            return result;
        }
    }
}