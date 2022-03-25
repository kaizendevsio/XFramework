using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Wallet;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class LogoutHandler : ActionHandler<Logout>
    {
        public SessionState CurrentState => Store.GetState<SessionState>();
        
        public LogoutHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
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

        public override async Task<Unit> Handle(Logout action, CancellationToken aCancellationToken)
        {
            if (action.ResetAllStates)
            {
                await SessionStorageService.ClearAsync();
                Store.Reset();
            }
            else
            {
                await SessionStorageService.RemoveItemAsync("SessionState");
                await SessionStorageService.RemoveItemAsync("WalletState");
                
                await LocalStorageService.RemoveItemAsync("SessionState");
                await LocalStorageService.RemoveItemAsync("WalletState");

                await Mediator.Send(new SetState()
                {
                    State = new(),
                    ContactList = new(),
                    Credential = new(),
                    Identity = new(),
                    LoginVm = new(),
                    RegisterVm = new(),
                    ForgotPasswordVm = new(),
                    NavigationHistoryList = new()
                });
                await Mediator.Send(new WalletState.SetState
                {
                    WalletList = new()
                });
            }
            
            if (string.IsNullOrEmpty(action.NavigateTo))
            {
                action.NavigateTo = "/";
            }
            NavigationManager.NavigateTo(action.NavigateTo);
            return Unit.Value;
        }
    }
}