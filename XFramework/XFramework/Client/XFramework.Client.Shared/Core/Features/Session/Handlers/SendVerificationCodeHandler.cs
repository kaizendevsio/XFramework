using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class SendVerificationCodeHandler : ActionHandler<SendVerificationCode, CmdResponse>
    {
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();

        public SendVerificationCodeHandler(IIdentityServiceWrapper identityServiceWrapper ,IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            IdentityServiceWrapper = identityServiceWrapper;
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

        public override async Task<CmdResponse> Handle(SendVerificationCode action, CancellationToken aCancellationToken)
        {
            var response = await IdentityServiceWrapper.UpdateVerification(new()
            {
                VerificationCode = CurrentState.SmsVerificationVm.OtpCode
            });
            
            if(await HandleFailure(response, action, true ,"Your otp code is incorrect. Please try again")) return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                IsSuccess = false
            };
            
            await HandleSuccess(response, action, true);
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true
            };
        }
    }
}