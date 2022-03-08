using Blazored.LocalStorage;
using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using IdentityServer.Domain.Generic.Contracts.Requests.Create;
using Mapster;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class RegisterActionHandler : ActionHandler<Register>
    {
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();

        public RegisterActionHandler(IIdentityServiceWrapper identityServiceWrapper ,IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(Register action, CancellationToken aCancellationToken)
        {
            // Inform UI About Busy State
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = true});
            
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
            if (await HandleFailure(identity, action)) return Unit.Value;
            
            // Send Create Credential Request
            var credentialRequest = CurrentState.RegisterVm.Adapt<CreateCredentialRequest>();
            credentialRequest.Guid = credentialGuid;
            credentialRequest.IdentityGuid = identityGuid;
            credentialRequest.RoleEntity = Guid.Parse("fb2ec753-66b2-4259-b65f-1c6402e58209");
            
            var credential = await IdentityServiceWrapper.CreateCredential(credentialRequest);
            if (await HandleFailure(credential, action)) return Unit.Value;
            
            // Send Create Phone Contact Request
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.PhoneNumber))
            {
                var phoneContact = await IdentityServiceWrapper.CreateContact(new()
                {
                    CredentialGuid = credentialGuid,
                    ContactType = GenericContactType.Phone,
                    Value = CurrentState.RegisterVm.PhoneNumber
                });
                if (await HandleFailure(phoneContact, action)) return Unit.Value;
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
                if (await HandleFailure(emailContact, action)) return Unit.Value;
            }
            
            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(credential, action);

            // Inform UI About Not Busy State
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
            
            return Unit.Value;
        }

        private async Task<bool> CheckDuplicateRecords(Register action)
        {
            // Check Identity Duplicates
            var identityExistence = await IdentityServiceWrapper.CheckIdentityExistence(CurrentState.RegisterVm.Adapt<CheckIdentityExistenceRequest>());
            if (await HandleFailure(identityExistence, action)) return true;

            // Check Credential Duplicates
            var credentialExistence = await IdentityServiceWrapper.CheckCredentialExistence(CurrentState.RegisterVm.Adapt<CheckCredentialExistenceRequest>());
            if (await HandleFailure(credentialExistence, action)) return true;

            // Check Phone Number Duplicates
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.PhoneNumber))
            {
                var phoneExistence = await IdentityServiceWrapper.CheckContactExistence(new()
                {
                    ContactType = GenericContactType.Phone,
                    Value = CurrentState.RegisterVm.PhoneNumber
                });
                if (await HandleFailure(phoneExistence, action)) return true;
            }

            // Check Email Address Duplicates
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.EmailAddress))
            {
                var emailExistence = await IdentityServiceWrapper.CheckContactExistence(new()
                {
                    ContactType = GenericContactType.Email,
                    Value = CurrentState.RegisterVm.EmailAddress
                });
                if (await HandleFailure(emailExistence, action)) return true;
            }

            return false;
        }

    }
}