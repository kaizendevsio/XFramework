using Blazored.LocalStorage;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class ClearStateHandler : ActionHandler<ClearState>
    {
        private XFramework.Client.Shared.Core.Features.Layout.LayoutState CurrentState => Store.GetState<XFramework.Client.Shared.Core.Features.Layout.LayoutState>();
        
        public ClearStateHandler(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
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

        public override async Task<Unit> Handle(ClearState state, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.ClearProperties(state, CurrentState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return Unit.Value;
        }
    }
}