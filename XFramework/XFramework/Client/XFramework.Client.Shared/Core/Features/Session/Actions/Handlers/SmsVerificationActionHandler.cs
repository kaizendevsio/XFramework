namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class SmsVerificationActionHandler : ActionHandler<SmsVerificationAction>
    {
        public SmsVerificationActionHandler(ISessionStorageService sessionStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPointsModel, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, sweetAlertService, navigationManager, endPointsModel, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(SmsVerificationAction action, CancellationToken aCancellationToken)
        {
            Console.WriteLine($"SmsVerificationCode: {action.Request.SmsVerifyRequest}");
            NavigationManager.NavigateTo(action.NavigateTo);
            
            return Unit.Value;
        }
    }
}
