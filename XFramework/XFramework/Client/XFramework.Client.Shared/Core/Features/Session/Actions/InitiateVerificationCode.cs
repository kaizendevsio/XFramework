using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using XFramework.Client.Shared.Entity.Models.Requests.Common;
using XFramework.Client.Shared.Entity.Models.Requests.Session;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class InitiateVerificationCode : NavigableRequest, IRequest<CmdResponse>
    {
        public Guid? CredentialGuid { get; set; }
        public Action OnSuccess { get; set; }
        public Action OnFailure { get; set; }
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
        : ActionHandler<InitiateVerificationCode, CmdResponse>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task<CmdResponse> Handle(InitiateVerificationCode action, CancellationToken aCancellationToken)
        {
            if (!hostEnvironment.IsProduction())
            {
                NavigateTo(action.NavigateToOnSuccess);
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Accepted,
                    
                };
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

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}