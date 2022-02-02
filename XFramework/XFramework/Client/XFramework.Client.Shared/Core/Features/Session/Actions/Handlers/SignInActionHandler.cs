namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class SignInActionHandler : ActionHandler<SignInAction>
    {
        public SignInActionHandler(ISessionStorageService sessionStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPointsModel, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, sweetAlertService, navigationManager, endPointsModel, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(SignInAction action, CancellationToken aCancellationToken)
        {
            Console.WriteLine($"Email: {action.Request.Email}");
            Console.WriteLine($"Password: {action.Request.Password}");
            //_navigationManager.NavigateTo(action.NavigateTo);

            /*var signInModel = SessionState.SignInRequest.
            Mediator.Send(new SessionState.SetAction(){LoginRequest = })*/

            return Unit.Value;
        }
    }
}