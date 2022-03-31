using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Wallet;

namespace XFramework.Client.Shared.Core.Features.Application;


public partial class ApplicationState
{

    public class RestoreStatesHandler : ActionHandler<RestoreStates>
    {
        public RestoreStatesHandler(IndexedDbService indexedDbService, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(RestoreStates action, CancellationToken aCancellationToken)
        {
            try
            { 
                await IndexedDbService.InitializeDb();
                
                StateHelper.RestoreState(Mediator, IndexedDbService,new ApplicationState.SetState() , ApplicationState);
                StateHelper.RestoreState(Mediator,IndexedDbService ,new SessionState.SetState() , SessionState);
                StateHelper.RestoreState(Mediator,IndexedDbService ,new WalletState.SetState() , WalletState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
            Console.WriteLine("State Restored");
            return Unit.Value;
        }
    }
}