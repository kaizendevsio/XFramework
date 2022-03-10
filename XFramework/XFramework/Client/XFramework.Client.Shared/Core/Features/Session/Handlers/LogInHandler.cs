using Blazored.LocalStorage;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using Mapster;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    protected class LogInHandler : ActionHandler<Login>
    {
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();
        
        public LogInHandler(IIdentityServiceWrapper identityServiceWrapper ,IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(Login action, CancellationToken aCancellationToken)
        {
            // Inform UI About Busy State
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = true});

            // Map view model to request object
            var request = CurrentState.LoginVm.Adapt<AuthenticateCredentialRequest>();
            
            // Send the request
            var response = await IdentityServiceWrapper.AuthenticateCredential(request);
            
            // Handle if the response is invalid or error
            if(await HandleFailure(response, action, true ,"There was an error while trying to sign you in. Please check your credentials and try again")) return Unit.Value;
           
            // Set Session State To Active
            await Mediator.Send(new SetState() {State = Domain.Generic.Enums.SessionState.Active});

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, true);
            
            // Fetch User Identity And Credential and Contact List
            var identityResponse = await IdentityServiceWrapper.GetIdentity(new() {Guid = response.Response.IdentityGuid});
            var credentialResponse = await IdentityServiceWrapper.GetCredential(new() {Guid = response.Response.CredentialGuid});
            var contactListResponse = await IdentityServiceWrapper.GetContactList(new() {CredentialGuid = response.Response.CredentialGuid});
            
            // Set State And Update UI
            await Mediator.Send(new SetState()
            {
                Identity = identityResponse.Response,
                Credential = credentialResponse.Response,
                ContactList = contactListResponse.Response
            });
            
            // Inform UI About Not Busy State
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
            
            return Unit.Value;
        }
    }
}