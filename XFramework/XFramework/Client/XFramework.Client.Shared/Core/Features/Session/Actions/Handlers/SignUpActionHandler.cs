namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState  
{
    public class SignUpActionHandler : ActionHandler<SignUpAction>
    {
        public SignUpActionHandler(ISessionStorageService sessionStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPointsModel, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, sweetAlertService, navigationManager, endPointsModel, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(SignUpAction action, CancellationToken aCancellationToken)
        {
            Console.WriteLine($"Email: {action.Request.RegisterEmail}");
            Console.WriteLine($"Email: {action.Request.RegisterMobile}");
            Console.WriteLine($"Password: {action.Request.RegisterPassword}");
            Console.WriteLine($"PasswordAgain: {action.Request.RegisterPasswordAgain}");
            NavigationManager.NavigateTo(action.NavigateTo);
            
            return Unit.Value;
        }
    }
}
