using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using IdentityServer.Domain.Generic.Contracts.Requests.Create;
using Mapster;
using XFramework.Client.Shared.Core.Features.Wallet;
using XFramework.Domain.Generic.Contracts.Responses;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    protected class RegisterActionHandler : ActionHandler<Register, CmdResponse>
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

        public override async Task<CmdResponse> Handle(Register action, CancellationToken aCancellationToken)
        {
            IsSilent = action.IsSilent;
            CurrentState.RegisterVm.RoleList ??= action.RoleList;
            
            // Inform UI About Busy State
            await ReportTask("Creating Account..", true);

            // Check If Any Given Data Are Already Existing
            if (await CheckDuplicateRecords(action)) return new()
            {
                HttpStatusCode = HttpStatusCode.NotAcceptable,
                IsSuccess = false
            };

            // Check If Passwords Are Correct
            if (!CurrentState.RegisterVm.Password.Equals(CurrentState.RegisterVm.PasswordConfirmation))
            {
                SweetAlertService.FireAsync("Password does not match",$"", SweetAlertIcon.Error);
                return new()
                {
                    HttpStatusCode = HttpStatusCode.BadRequest,
                    IsSuccess = false
                };
            }
            
            // Create Guids
            var identityGuid = Guid.NewGuid();
            var credentialGuid = Guid.NewGuid();

            // Send Create Identity Request
            await ReportTask("Creating identity..", null);
            var identityRequest = CurrentState.RegisterVm.Adapt<CreateIdentityRequest>();
            identityRequest.Guid = identityGuid;
            
            var identity = await IdentityServiceWrapper.CreateIdentity(identityRequest);
            if (await HandleFailure(identity, action)) return new()
            {
                HttpStatusCode = HttpStatusCode.InternalServerError,
                IsSuccess = false
            };;
            
            // Send Create Credential Request
            await ReportTask("Creating credential..", null);
            var credentialRequest = CurrentState.RegisterVm.Adapt<CreateCredentialRequest>();
            credentialRequest.Guid = credentialGuid;
            credentialRequest.IdentityGuid = identityGuid;
            
            var credential = await IdentityServiceWrapper.CreateCredential(credentialRequest);
            if (await HandleFailure(credential, action)) return new()
            {
                HttpStatusCode = HttpStatusCode.InternalServerError,
                IsSuccess = false
            };
            
            // Send Create Phone Contact Request
            await ReportTask("Creating contacts..", null);
            var phoneContact = await IdentityServiceWrapper.CreateContact(new()
            {
                CredentialGuid = credentialGuid,
                ContactType = GenericContactType.Phone,
                Value = !string.IsNullOrEmpty(CurrentState.RegisterVm.PhoneNumber) ? CurrentState.RegisterVm.PhoneNumber : string.Empty,
                GroupGuid = Guid.Parse("b4bda700-03c1-4a8a-bf6d-6043704cf767"),
                SendOtp = !action.SkipVerification
            });
            if (await HandleFailure(phoneContact, action)) return new()
            {
                HttpStatusCode = HttpStatusCode.InternalServerError,
                IsSuccess = false
            };
            
            // Send Create Email Contact Request
            await ReportTask("Creating contacts..", null);
            var emailContact = await IdentityServiceWrapper.CreateContact(new()
            {
                CredentialGuid = credentialGuid,
                ContactType = GenericContactType.Email,
                Value = !string.IsNullOrEmpty(CurrentState.RegisterVm.EmailAddress) ? CurrentState.RegisterVm.EmailAddress : string.Empty,
                GroupGuid = Guid.Parse("b4bda700-03c1-4a8a-bf6d-6043704cf767")
            });
            if (await HandleFailure(emailContact, action)) return new()
            {
                HttpStatusCode = HttpStatusCode.InternalServerError,
                IsSuccess = false
            };

            // If WalletList property is provided, automatically create wallets
            if (action.WalletList is not null)
            {
                await ReportTask("Creating wallets..", null);
                await CreateWallets(action.WalletList, credentialGuid);
            }
            
            // If AutoLogin property is true, automatically log the identity in
            if (action.AutoLogin)
            {
                ReportTask("Logging In..", null);
                var username = string.Empty;
                if (!string.IsNullOrEmpty(CurrentState.RegisterVm.PhoneNumber))
                {
                    username = CurrentState.RegisterVm.PhoneNumber;
                }
                if (!string.IsNullOrEmpty(CurrentState.RegisterVm.EmailAddress))
                {
                    username = CurrentState.RegisterVm.EmailAddress;
                }
                if (!string.IsNullOrEmpty(CurrentState.RegisterVm.UserName))
                {
                    username = CurrentState.RegisterVm.UserName;
                }

                SessionState.LoginVm.Username = username; 
                SessionState.LoginVm.Password = CurrentState.RegisterVm.Password; 
                await Mediator.Send(new Login()
                {
                    NavigateToOnSuccess = action.NavigateToOnSuccess,
                    SkipVerification = action.SkipVerification,
                    Role = action.RoleList.First()
                });
            
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Accepted,
                    IsSuccess = true
                };
            }
            
            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(credential, action);

            // Inform UI About Not Busy State
            ReportTask("", false);
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true
            };;
        }

        private async Task CreateWallets(List<(Guid?, decimal)> walletList, Guid credentialGuid)
        {
            foreach (var (walletEntityGuid, balance) in walletList)
            {
                await Mediator.Send(new WalletState.CreateWallet
                {
                    CredentialGuid = credentialGuid,
                    WalletEntityGuid = (Guid) walletEntityGuid,
                    Balance = balance,
                    Silent = true
                });
            }
        }

        private async Task<bool> CheckDuplicateRecords(Register action)
        {
            // Check Identity Duplicates
            await ReportTask("Validating identity..", null);
            var identityExistence = await IdentityServiceWrapper.CheckIdentityExistence(CurrentState.RegisterVm.Adapt<CheckIdentityExistenceRequest>());
            if (await HandleFailure(identityExistence, action)) return true;

            // Check Credential Duplicates
            await ReportTask("Validating credentials..", null);
            var credentialExistence = await IdentityServiceWrapper.CheckCredentialExistence(CurrentState.RegisterVm.Adapt<CheckCredentialExistenceRequest>());
            if (await HandleFailure(credentialExistence, action)) return true;

            // Check Phone Number Duplicates
            await ReportTask("Checking for duplicate phone numbers..", null);
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.PhoneNumber))
            {
                var phoneExistence = await IdentityServiceWrapper.CheckContactExistence(new()
                {
                    ContactType = GenericContactType.Phone,
                    Value = CurrentState.RegisterVm.PhoneNumber,
                    GroupGuid = Guid.Parse("b4bda700-03c1-4a8a-bf6d-6043704cf767"),
                });
                if (await HandleFailure(phoneExistence, action)) return true;
            }

            // Check Email Address Duplicates
            await ReportTask("Checking for duplicate email address..", null);
            if (!string.IsNullOrEmpty(CurrentState.RegisterVm.EmailAddress))
            {
                var emailExistence = await IdentityServiceWrapper.CheckContactExistence(new()
                {
                    ContactType = GenericContactType.Email,
                    Value = CurrentState.RegisterVm.EmailAddress,
                    GroupGuid = Guid.Parse("b4bda700-03c1-4a8a-bf6d-6043704cf767"),
                });
                if (await HandleFailure(emailExistence, action)) return true;
            }

            return false;
        }

    }
}