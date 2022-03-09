using Blazored.LocalStorage;
using Mapster;
using Microsoft.Extensions.Configuration;
using Wallets.Domain.Generic.Contracts.Requests.Create;
using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class CreateWalletHandler : ActionHandler<CreateWallet>
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
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = true});

            // Map view model to request object
            var request = action.Adapt<CreateWalletRequest>();
            request.CredentialGuid = SessionState.Credential.Guid;
            
            // Send the request
            var response = await WalletServiceWrapper.CreateWallet(request);
            
            // Handle if the response is invalid or error
            if(await HandleFailure(response, action, true ,"There was an error while trying to create your wallet. Please check your internet connection and try again")) return Unit.Value;

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, true);
            
            // Set State And Update UI
            if (action.ReloadWalletList)
            {
                await Mediator.Send(new GetWalletList());
            }
            
            // Inform UI About Not Busy State
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
            
            return Unit.Value;

        }
    }
}