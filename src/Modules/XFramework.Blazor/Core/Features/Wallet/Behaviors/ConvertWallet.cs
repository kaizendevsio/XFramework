using Wallets.Integration.Drivers;

namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState
{
    public record ConvertWallet : StateAction<CmdResponse>
    {
        public required Guid SourceWalletTypeId { get; set; }
        public required Guid TargetWalletTypeId { get; set; }
        public Guid CredentialId { get; set; }
        public decimal Amount { get; set; }
        public decimal Fee { get; set; }
        public decimal ConvenienceFee { get; set; }
        public string? Remarks { get; set; }
        public string? ClientReference { get; set; }
    };

    protected class ConvertWalletHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<ConvertWallet, CmdResponse>(handlerServices, store)
    {
        public WalletState CurrentState => Store.GetState<WalletState>();
        
        public override async Task<CmdResponse> Handle(ConvertWallet action, CancellationToken aCancellationToken)
        {
            var result = await walletsServiceWrapper.ConvertWallet(new()
            {
                ReferenceNumber = action.ClientReference,
                CredentialId = action.CredentialId,
                SourceWalletTypeId = action.SourceWalletTypeId,
                TargetWalletTypeId = action.TargetWalletTypeId,
                Amount = action.Amount,
                Fee = action.Fee,
                Remarks = action.Remarks,
                CurrencyId = Constants.Currency.Php
            });

            if (await HandleFailure(result, action)) return result;

            _ = Mediator.Send(new LoadWalletList(), CancellationToken.None);

            await HandleSuccess(result, action, false, "Wallet Conversion Successful");
            return result;
        }
    }

}