using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record ResendVerificationCode : StateAction
    {
        public Guid CredentialId { get; set; }
        public Guid VerificationTypeId { get; set; }
    }
    
    protected class ResendVerificationCodeHandler(IMessagingServiceWrapper messagingServiceWrapper,
        IWebAssemblyHostEnvironment hostEnvironment,
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<ResendVerificationCode>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task Handle(ResendVerificationCode action, CancellationToken aCancellationToken)
        {
            var result = await identityServerServiceWrapper.IdentityVerification.Create(new()
            {
                CredentialId = action.CredentialId,
                VerificationTypeId = action.VerificationTypeId
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
                
            SessionState.VerificationVm.Id = result.Response?.Id ?? Guid.Empty;
            SessionState.VerificationVm.CredentialId = action.CredentialId;
        }
    }
}