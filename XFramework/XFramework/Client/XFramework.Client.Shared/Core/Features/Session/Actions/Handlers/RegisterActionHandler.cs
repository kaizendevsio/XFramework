using Blazored.LocalStorage;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using IdentityServer.Domain.Generic.Contracts.Requests.Create;
using Mapster;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class RegisterActionHandler : ActionHandler<RegisterAction>
    {
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();

        public RegisterActionHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
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

        public override async Task<Unit> Handle(RegisterAction action, CancellationToken aCancellationToken)
        {
            // Check If Any Given Data Are Already Existing
            if (await CheckDuplicateRecords(action)) return Unit.Value;

            // Check If Passwords Are Correct
            if (!CurrentState.RegisterVm.Password.Equals(CurrentState.RegisterVm.PasswordConfirmation))
            {
                SweetAlertService.FireAsync("Password does not match",$"", SweetAlertIcon.Error);
                return Unit.Value;
            }
            
            // Create Guids
            var identityGuid = Guid.NewGuid();
            var credentialGuid = Guid.NewGuid();

            // Send Create Identity Request
            var identityRequest = CurrentState.RegisterVm.Adapt<CreateIdentityRequest>();
            identityRequest.Guid = identityGuid;
            
            var identity = await IdentityServiceWrapper.CreateIdentity(identityRequest);
            if (HandleFailure(identity, action)) return Unit.Value;
            
            // Send Create Credential Request
            var credentialRequest = CurrentState.RegisterVm.Adapt<CreateCredentialRequest>();
            credentialRequest.Guid = credentialGuid;
            credentialRequest.IdentityGuid = identityGuid;
            credentialRequest.RoleEntity = Guid.Parse("fb2ec753-66b2-4259-b65f-1c6402e58209");
            
            var credential = await IdentityServiceWrapper.CreateCredential(credentialRequest);
            if (HandleFailure(credential, action)) return Unit.Value;
            
            // Send Create Phone Contact Request
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.PhoneNumber))
            {
                var phoneContact = await IdentityServiceWrapper.CreateContact(new()
                {
                    CredentialGuid = credentialGuid,
                    ContactType = GenericContactType.Phone,
                    Value = CurrentState.RegisterVm.PhoneNumber
                });
                if (HandleFailure(phoneContact, action)) return Unit.Value;
            }
            
            // Send Create Email Contact Request
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.EmailAddress))
            {
                var emailContact = await IdentityServiceWrapper.CreateContact(new()
                {
                    CredentialGuid = credentialGuid,
                    ContactType = GenericContactType.Phone,
                    Value = CurrentState.RegisterVm.EmailAddress
                });
                if (HandleFailure(emailContact, action)) return Unit.Value;
            }
            
            // If Success URL property is provided, navigate to the given URL
            if (!string.IsNullOrEmpty(action.NavigateToOnSuccess))
            {
                NavigationManager.NavigateTo(action.NavigateToOnSuccess);
            }

            return Unit.Value;
        }

        private async Task<bool> CheckDuplicateRecords(RegisterAction action)
        {
            // Check Identity Duplicates
            var identityExistence = await IdentityServiceWrapper.CheckIdentityExistence(CurrentState.RegisterVm.Adapt<CheckIdentityExistenceRequest>());
            if (HandleFailure(identityExistence, action)) return true;

            // Check Credential Duplicates
            var credentialExistence = await IdentityServiceWrapper.CheckCredentialExistence(CurrentState.RegisterVm.Adapt<CheckCredentialExistenceRequest>());
            if (HandleFailure(credentialExistence, action)) return true;

            // Check Phone Number Duplicates
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.PhoneNumber))
            {
                var phoneExistence = await IdentityServiceWrapper.CheckContactExistence(new()
                {
                    ContactType = GenericContactType.Phone,
                    Value = CurrentState.RegisterVm.PhoneNumber
                });
                if (HandleFailure(phoneExistence, action)) return true;
            }

            // Check Email Address Duplicates
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.EmailAddress))
            {
                var emailExistence = await IdentityServiceWrapper.CheckContactExistence(new()
                {
                    ContactType = GenericContactType.Email,
                    Value = CurrentState.RegisterVm.EmailAddress
                });
                if (HandleFailure(emailExistence, action)) return true;
            }

            return false;
        }
        private bool HandleFailure(CmdResponseBO request, RegisterAction action)
        {
            if (request.HttpStatusCode is HttpStatusCode.Accepted) return false;
            SweetAlertService.FireAsync($"An error occured while creating your account: {request.Message}", $"", SweetAlertIcon.Error);
            
            // If Fail URL property is provided, navigate to the given URL
            if (!string.IsNullOrEmpty(action.NavigateToOnFailure))
            {
                NavigationManager.NavigateTo(action.NavigateToOnFailure);
            }
            return true;
        }
        private bool HandleFailure(QueryResponseBO<ExistenceResponse> request, RegisterAction action)
        {
            if (request.HttpStatusCode is HttpStatusCode.Accepted) return false;
            SweetAlertService.FireAsync($"An error occured while creating your account: {request.Message}", $"", SweetAlertIcon.Error);
            
            // If Fail URL property is provided, navigate to the given URL
            if (!string.IsNullOrEmpty(action.NavigateToOnFailure))
            {
                NavigationManager.NavigateTo(action.NavigateToOnFailure);
            }
            return true;
        }
    }
}