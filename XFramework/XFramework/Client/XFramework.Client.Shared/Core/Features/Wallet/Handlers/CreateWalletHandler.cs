using Mapster;
using Wallets.Domain.Generic.Contracts.Requests.Create;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    protected class CreateWalletHandler : ActionHandler<CreateWallet>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }
        public SessionState WalletState => Store.GetState<SessionState>();
        public CreateWalletHandler(IWalletServiceWrapper walletServiceWrapper ,IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            WalletServiceWrapper = walletServiceWrapper;
            Configuration = configuration;
            SessionStorageService = sessionStorageService;
            LocalStorageService = localStorageService;
            SweetAlertService = sweetAlertService;
            NavigationManager = navigationManager;
            EndPoints = endPoints;
            HttpClient = httpClient;
            BaseHttpClient = baseHttpClient;
            JsRuntime = jsRuntime;
            Mediator = mediator;
            Store = store;
        }

        public override async Task<Unit> Handle(CreateWallet action, CancellationToken aCancellationToken)
        {
            // Inform UI About Busy State
            if (!action.Silent)
            {
                ReportTask("Creating Wallet..", true);
            }

            // Check if CredentialGuid is provided
            if (SessionState.State is not CurrentSessionState.Active && action.CredentialGuid is null)
            {
                SweetAlertService.FireAsync("Error", "Could not create wallet, credentials not provided");
                return Unit.Value;
            }
            
            // Map view model to request object
            var request = action.Adapt<CreateWalletRequest>();
            request.CredentialGuid = SessionState.State is CurrentSessionState.Active ? SessionState.Credential.Guid : action.CredentialGuid;
            
            // Send the request
            var response = await WalletServiceWrapper.CreateWallet(request);
            
            if (!action.Silent)
            {
                // Handle if the response is invalid or error
                if (await HandleFailure(response, action, true, "There was an error while trying to create your wallet. Please check your internet connection and try again")) return Unit.Value;

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

            return Unit.Value;

        }
    }
}