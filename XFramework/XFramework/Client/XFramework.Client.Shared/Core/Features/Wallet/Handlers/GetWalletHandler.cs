namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    protected class GetWalletHandler : ActionHandler<GetWallet>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }

        public GetWalletHandler(IWalletServiceWrapper walletServiceWrapper, IConfiguration configuration,
            ISessionStorageService sessionStorageService, ILocalStorageService localStorageService,
            SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints,
            IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) :
            base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager,
                endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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
 
        public override async Task Handle(GetWallet action, CancellationToken aCancellationToken)
        {
            if(SessionState.State is not CurrentSessionState.Active) return;
            var response = await WalletServiceWrapper.GetWallet(new()
            {
                Guid = action.WalletGuid
            });

            // Handle if the response is invalid or error
            if (await HandleFailure(response, action, true)) return;

            // Set Session State To Active
            await Mediator.Send(new SetState() {SelectedWallet = response.Response});

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, true);

            return;
        }
    }
}