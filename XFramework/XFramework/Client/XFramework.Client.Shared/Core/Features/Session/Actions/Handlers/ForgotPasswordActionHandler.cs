using Blazored.LocalStorage;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using IdentityServer.Domain.Generic.Contracts.Requests.Update;
using Mapster;
using Microsoft.Extensions.Configuration;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Session;


public partial class SessionState
{
    public class ForgotPasswordActionHandler : ActionHandler<ForgotPasswordAction>
    {
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();

        
        public ForgotPasswordActionHandler(IIdentityServiceWrapper identityServiceWrapper ,IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(ForgotPasswordAction action, CancellationToken aCancellationToken)
        {
            // Map view model to request object
            var request = CurrentState.ForgotPasswordVm.Adapt<UpdatePasswordRequest>();
            
            // Send the request
            var response = await IdentityServiceWrapper.ChangePassword(request);
            
            // Handle if the response is invalid or error
            if(await HandleFailure(response, action, "There was an error while trying to sign you in. Please Try again later")) return Unit.Value;
            
            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action);
            
            return Unit.Value;
        }
    }
}

