namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class ForgotPasswordActionHandler : ActionHandler<ForgotPasswordAction>
    {
        private readonly NavigationManager _navigationManager;
        
        public ForgotPasswordActionHandler(ISessionStorageService sessionStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPointsModel, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, sweetAlertService, navigationManager, endPointsModel, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            SessionStorageService = sessionStorageService;
            SweetAlertService = sweetAlertService;
            NavigationManager = navigationManager;
            EndPoints = endPointsModel;
            HttpClient = httpClient;
            BaseHttpClient = baseHttpClient;
            JsRuntime = jsRuntime;
            Mediator = mediator;
            Store = store;
        }

        public override async Task<Unit> Handle(ForgotPasswordAction action, CancellationToken aCancellationToken)
        {
            Console.WriteLine($"ResetPassword: {action.Request.ResetPassword}");
            _navigationManager.NavigateTo(action.NavigateTo);
            
            return Unit.Value;
        }
    }
}
