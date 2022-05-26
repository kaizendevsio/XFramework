using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class InitiateResetPasswordHandler : ActionHandler<InitiateResetPassword, CmdResponse>
    {
        public IHelperService HelperService { get; }
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();
        public TaskCompletionSource TaskCompletionSource { get; set; } = new();
        
        public InitiateResetPasswordHandler(IHelperService helperService, IIdentityServiceWrapper identityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            HelperService = helperService;
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

        public override async Task<CmdResponse> Handle(InitiateResetPassword action, CancellationToken aCancellationToken)
        {
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = true});
            
            CurrentState.ForgotPasswordVm.PhoneNumber = CurrentState.ForgotPasswordVm.PhoneNumber.ValidatePhoneNumber();
            await Mediator.Send(new InitiateVerificationCode
            {
                LocalVerification = true,
                LocalToken = $"{HelperService.GenerateRandomNumber(111111, 999999)}",
                ContactType = GenericContactType.Phone,
                Contact = SessionState.ForgotPasswordVm.PhoneNumber.ValidatePhoneNumber(),
                NavigateToOnSuccess = action.NavigateToOnSuccess,
                NavigateToOnFailure = action.NavigateToOnFailure,
                NavigateToOnVerificationRequired = action.NavigateToOnVerificationRequired,
            });
            
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = false});
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true
            };
        }
    }
}