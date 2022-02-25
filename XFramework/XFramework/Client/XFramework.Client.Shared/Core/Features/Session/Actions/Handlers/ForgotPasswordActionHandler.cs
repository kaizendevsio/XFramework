using Blazored.LocalStorage;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using Mapster;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Session;


public partial class SessionState
{
    public class ForgotPasswordActionHandler : ActionHandler<ForgotPasswordAction>
    {
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();

        
        public ForgotPasswordActionHandler(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
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
            var request = CurrentState.ForgotPasswordVm.Adapt<AuthenticateCredentialRequest>();
            var response = await IdentityServiceWrapper.AuthenticateCredential(request);
            
            if (response.HttpStatusCode is not HttpStatusCode.Accepted)
            {
                // Display message to UI
                SweetAlertService.FireAsync("Error", "There was an error while trying to sign you in. Please Try again later");
                
                // Display error to the console
                Console.WriteLine($"Error from response: {response.Message}");
                
                // If NavigateToOnFailure property is set, navigate to the given URL
                if (!string.IsNullOrEmpty(action.NavigateToOnFailure))
                {
                    NavigationManager.NavigateTo(action.NavigateToOnFailure);
                }
            }
            
            // If NavigateToOnSuccess property is set, navigate to the given URL
            if (!string.IsNullOrEmpty(action.NavigateToOnSuccess))
            {
                NavigationManager.NavigateTo(action.NavigateToOnSuccess);
            }
            
            return Unit.Value;
        }
    }
}

