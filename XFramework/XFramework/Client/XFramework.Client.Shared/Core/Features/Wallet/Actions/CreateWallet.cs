using Wallets.Integration.Drivers;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public record CreateWallet : StateAction
    {
        public bool ReloadWalletList { get; set; }
        public Guid WalletTypeId { get; set; }
        public decimal Balance { get; set; } = 0;
        public Guid? CredentialId { get; set; }
        public bool Silent { get; set; }
    }
    
    protected class CreateWalletHandler(
        IWalletsServiceWrapper walletsServiceWrapper,
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
                ReportTask("Creating Wallet..", true);
            }

            // Check if CredentialGuid is provided
            if (SessionState.State is not CurrentSessionState.Active && action.CredentialId is null)
            {
                SweetAlertService.FireAsync("Error", "Could not create wallet, credentials not provided");
                return;
            }
            
            // Map view model to request object
            var request = action.Adapt<Domain.Generic.Contracts.Wallet>();
            request.CredentialId = (Guid)(SessionState.State is CurrentSessionState.Active ? SessionState.Credential.Id : action.CredentialId);
            
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
                await Mediator.Send(new GetWalletList());
            }
            
            // Inform UI About Not Busy State
            if (!action.Silent)
            {
                ReportTask("Done", false);
            }

            return;

        }
    }
}