using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Wallet;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    protected class LogoutHandler : ActionHandler<Logout>
    {
        public SessionState CurrentState => Store.GetState<SessionState>();
        
        public LogoutHandler(IndexedDbService indexedDbService , IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            IndexedDbService = indexedDbService;
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
                IndexedDbService.Database.StateCache.Clear();
                await IndexedDbService.Database.SaveChanges();
                Store.Reset();
            }
            else
            {
                await IndexedDbService.RemoveItem("SessionState");
                await IndexedDbService.RemoveItem("WalletState");

                await Mediator.Send(new ClearState()
                {
                    ContactList = new(),
                    Credential = new(),
                    Identity = new()
                });
                await Mediator.Send(new SetState()
                {
                    State = Domain.Generic.Enums.SessionState.Inactive,
                    LoginVm = new(),
                    RegisterVm = new(),
                    ForgotPasswordVm = new()
                });
                
                await Mediator.Send(new WalletState.ClearState
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