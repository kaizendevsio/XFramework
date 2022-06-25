using Blazored.LocalStorage;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using IdentityServer.Domain.Generic.Contracts.Requests.Update;
using Mapster;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;
using XFramework.Integration.Services;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Client.Shared.Core.Features.Session;


public partial class SessionState
{
    protected class ResetPasswordHandler : ActionHandler<ResetPassword, CmdResponse>
    {
        public IHelperService HelperService { get; }
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();
        
        public ResetPasswordHandler(IHelperService helperService, IIdentityServiceWrapper identityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<CmdResponse> Handle(ResetPassword action, CancellationToken aCancellationToken)
        {
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = true});

            // Map view model to request object
            var request = CurrentState.ForgotPasswordVm.Adapt<UpdatePasswordRequest>();
            
            // Send the request
            var response = await IdentityServiceWrapper.ChangePassword(request);

            CurrentState.ForgotPasswordVm = new();
            // Handle if the response is invalid or error
            if (await HandleFailure(response, action, true, "There was an error while trying to reset your password. Please Try again later"))
            {
                return new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    IsSuccess = false
                };
            };
            
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = false});

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, false, "Your password has been reset successfully. Please login with your new password");
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true
            };
        }

    }
}

