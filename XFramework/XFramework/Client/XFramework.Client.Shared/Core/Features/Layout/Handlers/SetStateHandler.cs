using XFramework.Client.Shared.Entity.Enums;

namespace XFramework.Client.Shared.Core.Features.Layout;

public partial class LayoutState
{
    protected class SetStateHandler : ActionHandler<SetState>
    {
        private LayoutState CurrentState => Store.GetState<LayoutState>();
            
        public SetStateHandler(IndexedDbService indexedDbService, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(SetState action, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.SetProperties(action, CurrentState);
                CurrentState.LayoutIntent = action.LayoutIntent != LayoutIntent.NotSpecified
                    ? action.LayoutIntent
                    : CurrentState.LayoutIntent;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return Unit.Value;
        }
    }
}