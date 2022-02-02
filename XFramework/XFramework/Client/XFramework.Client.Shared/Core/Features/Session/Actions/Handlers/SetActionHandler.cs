// ReSharper disable once CheckNamespace
namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class SetActionHandler : ActionHandler<SetAction>
    {
        private SessionState CurrentState => Store.GetState<SessionState>();
        
        public SetActionHandler(ISessionStorageService sessionStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPointsModel, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, sweetAlertService, navigationManager, endPointsModel, httpClient, baseHttpClient, jsRuntime, mediator, store)
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
        
        public override async Task<Unit> Handle(SetAction action, CancellationToken aCancellationToken)
        {
            //StateHelper.SetProperties(action, CurrentState);
            CurrentState.SignInRequest = action.LoginRequest ?? CurrentState.SignInRequest;
            CurrentState.SignUpRequest = action.SignUpRequest ?? CurrentState.SignUpRequest;
            CurrentState.ForgotPasswordRequest = action.ForgotPasswordRequest ?? CurrentState.ForgotPasswordRequest;
            CurrentState.SmsVerificationRequest = action.SmsVerificationRequest ?? CurrentState.SmsVerificationRequest;
            
            
            return Unit.Value;
        }
    }
}