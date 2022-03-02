using Blazored.LocalStorage;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using Mapster;
using Microsoft.Extensions.Configuration;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class LogInActionHandler : ActionHandler<LoginAction>
    {
        public IConfiguration Configuration { get; }
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();
        
        public LogInActionHandler(IIdentityServiceWrapper identityServiceWrapper ,IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(LoginAction action, CancellationToken aCancellationToken)
        {
            // Map view model to request object
            var request = CurrentState.LoginVm.Adapt<AuthenticateCredentialRequest>();
            
            // Send the request
            var response = await IdentityServiceWrapper.AuthenticateCredential(request);
            
            // Handle if the response is invalid or error
            if(await HandleFailure(response, action, true ,"There was an error while trying to sign you in. Please Try again later")) return Unit.Value;
           
            // Set Session State To Active
            await Mediator.Send(new SetState() {State = Domain.Generic.Enums.SessionState.Active});

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, true);
            
            // Fetch User Identity And Credential and Contact List
            var identityResponse = await IdentityServiceWrapper.GetIdentity(new() {Guid = response.Response.IdentityGuid});
            var credentialResponse = await IdentityServiceWrapper.GetCredential(new() {Guid = response.Response.CredentialGuid});
            var contactListResponse = await IdentityServiceWrapper.GetContactList(new() {CredentialGuid = response.Response.CredentialGuid});
            
            
            await Mediator.Send(new SetState()
            {
                Identity = identityResponse.Response,
                Credential = credentialResponse.Response,
                ContactList = contactListResponse.Response
            });
            
            return Unit.Value;
        }
    }
}