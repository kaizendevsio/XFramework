using Blazored.LocalStorage;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using IdentityServer.Domain.Generic.Contracts.Responses.Verification;
using Mapster;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Client.Shared.Core.Features.Wallet;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    protected class LogInHandler : ActionHandler<Login, CmdResponse>
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

        public override async Task<CmdResponse> Handle(Login action, CancellationToken aCancellationToken)
        {
            // Inform UI About Busy State
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = true});

            // Map view model to request object
            var request = CurrentState.LoginVm.Adapt<AuthenticateCredentialRequest>();
            
            // Send the request
            var response = await IdentityServiceWrapper.AuthenticateCredential(request);
            
            // Handle if the response is invalid or error
            if(await HandleFailure(response, action, true ,"There was an error while trying to sign you in. Please check your credentials and try again")) return new()
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                IsSuccess = false
            };
            
            var checkVerification = new QueryResponse<IdentityVerificationSummaryResponse>();
            if (!action.SkipVerification)
            {
                checkVerification = await IdentityServiceWrapper.CheckVerification(new()
                {
                    CredentialGuid = response.Response.CredentialGuid,
                    VerificationTypeGuid = Guid.Parse("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4")
                });

                if (checkVerification.HttpStatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Accepted))
                {
                    if(await HandleFailure(checkVerification, action, true ,"There was an error while trying to sign you in: Account verification failure. Please try again")) return new()
                    {
                        HttpStatusCode = HttpStatusCode.BadRequest,
                        IsSuccess = false
                    };
                }

                if (checkVerification.HttpStatusCode is HttpStatusCode.NotFound || !checkVerification.Response.IsVerified)
                {
                    await IdentityServiceWrapper.CreateVerification(new()
                    {
                        CredentialGuid = response.Response.CredentialGuid,
                        VerificationTypeGuid = Guid.Parse("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4")
                    });
                }
            }         

            // Set Session State To Active
            await Mediator.Send(new SetState() {State = Domain.Generic.Enums.SessionState.Active});
            
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

            Console.WriteLine(JsonSerializer.Serialize(checkVerification));
            
            if (action.SkipVerification)
            {
                // If Success URL property is provided, navigate to the given URL
                await HandleSuccess(response, action, true);
            }
            else
            {
                if (checkVerification.HttpStatusCode is HttpStatusCode.NotFound || !checkVerification.Response.IsVerified)
                {
                    NavigationManager.NavigateTo(action.NavigateToOnVerificationRequired);
                }
                else
                {
                    // If Success URL property is provided, navigate to the given URL
                    await HandleSuccess(response, action, true);
                }
            }

            // Reset Session Forms
            await Mediator.Send(new SetState()
            {
                LoginVm = new(),
                RegisterVm = new(),
                ForgotPasswordVm = new()
            });

            // Initialize Wallets If Specified
            if (action.InitializeWallets)
            {
                try
                {
                    await Mediator.Send(new WalletState.GetWalletList());
                    if (action.AutoRefreshWallets)
                    {
                       WalletState.Timer = new Timer(_ =>
                        {
                            Task.Run(async () =>
                            {
                                await Mediator.Send(new WalletState.GetWalletList());
                            });
                        }, null, action.AutoRefreshWalletsInterval, action.AutoRefreshWalletsInterval);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

            // Inform UI About Not Busy State
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true
            };
        }
    }
}