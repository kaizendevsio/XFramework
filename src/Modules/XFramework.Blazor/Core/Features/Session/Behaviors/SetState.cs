using XFramework.Blazor.Entity.Models.Requests.Session;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record SetState : StateAction
    {
        public CurrentSessionState? State { get; set; }
        public List<IdentityContact>? ContactList { get; set; }
        public IdentityCredential? Credential { get; set; }
        public IdentityInformation? Identity { get; set; }
        public SignInRequest? LoginVm { get; set; }
        public SignUpRequest? RegisterVm { get; set; }
        public ResetPasswordRequest? ResetPasswordVm { get; set; }
        public VerificationRequest? VerificationVm { get; set; }
        public List<string>? NavigationHistoryList { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public Guid? SessionId { get; set; }
    }
    
    protected class SetStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<SetState>(handlerServices, store)
    {
        private SessionState CurrentState => Store.GetState<SessionState>();
        
        public override async Task Handle(SetState state, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.SetProperties(state, CurrentState);
                Persist(CurrentState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return;
        }
    }
} 
