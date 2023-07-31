using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using IdentityServer.Domain.Generic.Contracts.Requests.Update;
using Messaging.Integration.Interfaces;
using XFramework.Client.Shared.Entity.Models.Requests.Session;

namespace XFramework.Client.Shared.Core.Features.Session;


public partial class SessionState
{
    protected class InitiateVerificationCodeHandler : ActionHandler<InitiateVerificationCode, CmdResponse>
    {
        public IWebAssemblyHostEnvironment HostEnvironment { get; }
        public IMessagingServiceWrapper MessagingServiceWrapper { get; }
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();

        
        public InitiateVerificationCodeHandler(IWebAssemblyHostEnvironment hostEnvironment, IMessagingServiceWrapper messagingServiceWrapper ,IIdentityServiceWrapper identityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            HostEnvironment = hostEnvironment;
            MessagingServiceWrapper = messagingServiceWrapper;
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

        public override async Task<CmdResponse> Handle(InitiateVerificationCode action, CancellationToken aCancellationToken)
        {
            if (!HostEnvironment.IsProduction())
            {
                await NavigateTo(action.NavigateToOnSuccess);
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Accepted,
                    
                };
            }
             
            if (action.LocalVerification is true)
            {
                await MessagingServiceWrapper.CreateVerificationMessage(new()
                {
                    VerificationToken = action.LocalToken,
                    ContactType = action.ContactType,
                    Contact = action.Contact
                });
            }
            else
            {
                await IdentityServiceWrapper.CreateVerification(new()
                {
                    CredentialGuid = action.CredentialGuid,
                    VerificationTypeGuid = Guid.Parse("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4")
                });
            }

            SessionState.VerificationVm = action.Adapt<VerificationRequest>();
            NavigateTo(action.NavigateToOnVerificationRequired);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                
            };
        }
    }
}

