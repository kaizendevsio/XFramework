using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Community;
using XFramework.Client.Shared.Core.Features.Wallet;
using XFramework.Client.Shared.Entity.Enums;

namespace XFramework.Client.Shared.Core.Features.Application;


public partial class ApplicationState
{

    protected class RestoreStatesHandler : ActionHandler<RestoreStates>
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
                var statePersistenceFromAppSettings = Configuration.GetValue<string>("Application:Persistence:State:Driver");
                var persistStateBy = (PersistStateBy)Enum.Parse(typeof(PersistStateBy), statePersistenceFromAppSettings);

                if (persistStateBy is PersistStateBy.IndexDb)
                {
                    await IndexedDbService.InitializeDb();
                }

                StateHelper.RestoreState(Mediator, IndexedDbService ,SessionStorageService, LocalStorageService, new ApplicationState.SetState() , ApplicationState, persistStateBy);
                StateHelper.RestoreState(Mediator, IndexedDbService ,SessionStorageService, LocalStorageService,new SessionState.SetState() , SessionState, persistStateBy);
                StateHelper.RestoreState(Mediator, IndexedDbService ,SessionStorageService, LocalStorageService,new WalletState.SetState() , WalletState, persistStateBy);
                StateHelper.RestoreState(Mediator, IndexedDbService ,SessionStorageService, LocalStorageService,new CommunityState.SetState() , CommunityState, persistStateBy);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
            return Unit.Value;
        }
    }
}