using IdentityServer.Domain.Shared;
using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using XFramework.Blazor.Entity.Models.Requests.Common;
using XFramework.Blazor.Entity.Models.Requests.Session;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record InitiateVerificationCode : NavigableRequest
    {
        public Guid CredentialId { get; set; }
        public GenericContactType ContactType { get; set; }
        public string Contact { get; set; }
        public bool? LocalVerification { get; set; }
        public string LocalToken { get; set; }
    }
    
    protected class InitiateVerificationCodeHandler
    (
        IMessagingServiceWrapper messagingServiceWrapper,
        IWebAssemblyHostEnvironment hostEnvironment,
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<InitiateVerificationCode>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task Handle(InitiateVerificationCode action, CancellationToken aCancellationToken)
        {
            if (!hostEnvironment.IsProduction())
            {
                NavigateTo(action.NavigateToOnSuccess);
            }
             
            if (action.LocalVerification is true)
            {
                await messagingServiceWrapper.CreateVerificationMessage(new()
                {
                    VerificationToken = action.LocalToken,
                    ContactType = action.ContactType,
                    Contact = action.Contact
                });
                
                SessionState.VerificationVm = action.Adapt<VerificationRequest>();
            }
            else
            {
                var result = await identityServerServiceWrapper.IdentityVerification.Create(new()
                {
                    CredentialId = action.CredentialId,
                    VerificationTypeId = IdentityConstants.VerificationType.Sms
                });

                if (result.IsSuccess is false)
                {
                    await SweetAlertService.FireAsync(new()
                    {
                        Title = "Error",
                        Text = result.Message,
                        Icon = SweetAlertIcon.Error,
                        ShowCloseButton = true,
                        ConfirmButtonText = "Close",
                    });
                    return;
                }
                
                SessionState.VerificationVm = action.Adapt<VerificationRequest>();
                SessionState.VerificationVm.Id = result.Response?.Id ?? Guid.Empty;
                SessionState.VerificationVm.CredentialId = action.CredentialId;
            }

            NavigateTo(action.NavigateToOnVerificationRequired);
        }
    }
}