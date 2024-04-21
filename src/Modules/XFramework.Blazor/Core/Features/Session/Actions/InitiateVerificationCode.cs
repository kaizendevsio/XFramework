using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using XFramework.Blazor.Entity.Models.Requests.Common;
using XFramework.Blazor.Entity.Models.Requests.Session;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record InitiateVerificationCode : NavigableRequest
    {
        public Guid? CredentialGuid { get; set; }
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
            }
            else
            {
                await identityServerServiceWrapper.IdentityVerification.Create(new()
                {
                    CredentialId = (Guid)action.CredentialGuid,
                    VerificationTypeId = Guid.Parse("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4")
                });
            }

            SessionState.VerificationVm = action.Adapt<VerificationRequest>();
            NavigateTo(action.NavigateToOnVerificationRequired);
        }
    }
}