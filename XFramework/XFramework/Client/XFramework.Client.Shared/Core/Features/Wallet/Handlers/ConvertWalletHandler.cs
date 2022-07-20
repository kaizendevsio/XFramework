namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class ConvertWalletHandler : ActionHandler<ConvertWallet>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }
        public WalletState CurrentState => Store.GetState<WalletState>();

        public ConvertWalletHandler(IWalletServiceWrapper walletServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(ConvertWallet action, CancellationToken aCancellationToken)
        {
            throw new NotImplementedException();
        }
       
    }
}