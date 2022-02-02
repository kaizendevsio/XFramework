namespace XFramework.Client.Shared.Core.Features.Layout;

public partial class LayoutState
{
    public class ClearStateHandler : ActionHandler<ClearStateAction>
    {
        private XFramework.Client.Shared.Core.Features.Layout.LayoutState CurrentState => Store.GetState<XFramework.Client.Shared.Core.Features.Layout.LayoutState>();
        
        public ClearStateHandler(ISessionStorageService sessionStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            SessionStorageService = sessionStorageService;
            SweetAlertService = sweetAlertService;
            NavigationManager = navigationManager;
            EndPoints = endPoints;
            HttpClient = httpClient;
            BaseHttpClient = baseHttpClient;
            JsRuntime = jsRuntime; 
            Mediator = mediator;
            Store = store;
        }

        public override async Task<Unit> Handle(ClearStateAction action, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.ClearProperties(action, CurrentState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return Unit.Value;
        }
    }
}