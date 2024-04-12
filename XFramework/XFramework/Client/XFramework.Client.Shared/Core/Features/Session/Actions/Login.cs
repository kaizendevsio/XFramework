using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using XFramework.Client.Shared.Core.Features.Wallet;
using XFramework.Client.Shared.Entity.Models.Requests.Common;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public record Login : NavigableRequest
    {
        public bool SkipVerification { get; set; }
        public bool InitializeWallets { get; set; }
        public bool AutoRefreshWallets { get; set; }
        public TimeSpan AutoRefreshWalletsInterval { get; set; }
        public Guid Role { get; set; }
    }

    protected class LogInHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<Login>(handlerServices, store)
    {
        private SessionState CurrentState => Store.GetState<SessionState>();
        private bool VerificationRequired { get; set; }

        public override async Task Handle(Login action, CancellationToken aCancellationToken)
        {
            // Inform UI About Busy State
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = true});

            // Map view model to request object
            var request = CurrentState.LoginVm.Adapt<AuthenticateIdentityRequest>();
            request.RoleId = action.Role;
            
            // Send the request
            var response = await identityServerServiceWrapper.AuthenticateIdentity(request);

            // Handle if the response is invalid or error
            if (await HandleFailure(response, action)) return;
            
            var checkVerification = new QueryResponse<CheckVerificationResponse>();
            if (!action.SkipVerification && HostEnvironment.IsProduction())
            {
                checkVerification = await identityServerServiceWrapper.CheckVerification(new()
                {
                    CredentialId = response.Response.Credential.Id,
                    VerificationTypeId = Guid.Parse("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4")
                });

                if (checkVerification.HttpStatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Accepted))
                {
                    /*if(await HandleFailure(checkVerification, action, true ,"There was an error while trying to sign you in: Account verification failure. Please try again")) return new()
                    {
                        HttpStatusCode = HttpStatusCode.BadRequest,                        
                    };*/
                }

                if (checkVerification.HttpStatusCode is HttpStatusCode.NotFound || !checkVerification.Response.IsVerified)
                {
                    VerificationRequired = true;
                    await Mediator.Send(new InitiateVerificationCode
                    {
                        CredentialGuid = response.Response.Credential.Id,
                        NavigateToOnSuccess = action.NavigateToOnSuccess,
                        NavigateToOnFailure = action.NavigateToOnFailure,
                        NavigateToOnVerificationRequired = action.NavigateToOnVerificationRequired
                    });
                }
            }         

            // Set Session State To Active
            await Mediator.Send(new SetState()
            {
                Identity = response.Response.Identity,
                Credential = response.Response.Credential,
                State = CurrentSessionState.Active
            });
            
            // Broadcast login event
            Mediator.Publish(new LoginEvent
            {
                StatusCode = response.HttpStatusCode,
                Data = response.Response
            });
            
            // Fetch User Identity And Credential and Contact List
            var credentialResponse = identityServerServiceWrapper.IdentityCredential.Get(
                id: CurrentState.Credential.Id,
                includeNavigations: true,
                includes:[
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.AddressType)}",
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.Country)}",
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.Region)}",
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.Province)}",
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.City)}",
                    $"{nameof(IdentityCredential.IdentityInfo)}.{nameof(IdentityInformation.IdentityAddresses)}.{nameof(IdentityAddress.Barangay)}",
                    $"{nameof(IdentityCredential.IdentityContacts)}.{nameof(IdentityContact.Type)}",
                ]
            );
            var contactListResponse = identityServerServiceWrapper.IdentityContact.GetList(pageNumber: 1, pageSize: 100,
                filter: new() { 
                    new()
                    {
                        PropertyName = nameof(IdentityContact.CredentialId),
                        Operation = QueryFilterOperation.Equal,
                        Value = response.Response.Credential.Id
                    }
                }, 
                includeNavigations: true);

            await Task.WhenAll(credentialResponse, contactListResponse);
            
            // Set State And Update UI
            await Mediator.Send(new SetState()
            {
                Identity = credentialResponse.Result.Response.IdentityInfo,
                Credential = credentialResponse.Result.Response,
                ContactList = contactListResponse.Result.Response?.Items.ToList(),
            });

            if (!HostEnvironment.IsProduction())
            {
                // If Success URL property is provided, navigate to the given URL
                await HandleSuccess(response, action, true);
            }
            else if (action.SkipVerification || (checkVerification.HttpStatusCode is not HttpStatusCode.NotFound && checkVerification.Response.IsVerified))
            {
                // If Success URL property is provided, navigate to the given URL
                await HandleSuccess(response, action, true);
            }
            
            // Reset Session Forms
            await Mediator.Send(new SetState()
            {
                LoginVm = new(),
                RegisterVm = new(),
                ResetPasswordVm = new()
            });

            // Initialize Wallets If Specified
            if (action.InitializeWallets)
            {
                try
                {
                    await Mediator.Send(new WalletState.LoadWalletList());
                    if (action.AutoRefreshWallets)
                    {
                       WalletState.Timer = new Timer(_ =>
                        {
                            Task.Run(async () =>
                            {
                                await Mediator.Send(new WalletState.LoadWalletList());
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
        }
    }
}