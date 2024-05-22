using Wallets.Integration.Drivers;
using XFramework.Integration.Abstractions;

namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState
{
    public record CreateWallet : StateAction
    {
        public bool ReloadWalletList { get; set; }
        public Guid WalletTypeId { get; set; }
        public decimal Balance { get; set; } = 0;
        public Guid CredentialId { get; set; }
        public bool Silent { get; set; }
    }
    
    protected class CreateWalletHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
        IHelperService helperService,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<CreateWallet>(handlerServices, store)
    {
        public SessionState WalletState => Store.GetState<SessionState>();
        
        public override async Task Handle(CreateWallet action, CancellationToken aCancellationToken)
        {
            // Inform UI About Busy State
            if (!action.Silent)
            {
                ReportBusy("Creating Wallet..", true);
            }

            // Check if CredentialGuid is provided
            if (action.CredentialId == Guid.Empty)
            {
                SweetAlertService.FireAsync("Error", "Could not create wallet, credentials not provided");
                return;
            }
            
            // Map view model to request object
            var request = action.Adapt<Domain.Shared.Contracts.Wallet>();
            request.CredentialId = action.CredentialId;
            
            // Send the request
            var response = await walletsServiceWrapper.Wallet.Create(request);
            
            if (!action.Silent)
            {
                // Handle if the response is invalid or error
                if (await HandleFailure(response, action, true, "There was an error while trying to create your wallet. Please check your internet connection and try again")) return;

                // If Success URL property is provided, navigate to the given URL
                await HandleSuccess(response, action, true);
            }

            // Set State And Update UI
            if (action.ReloadWalletList)
            {
                await Mediator.Send(new LoadWalletList());
            }
            
            // Inform UI About Not Busy State
            if (!action.Silent)
            {
                ReportBusy("Done", false);
            }

            return;

        }
    }
}