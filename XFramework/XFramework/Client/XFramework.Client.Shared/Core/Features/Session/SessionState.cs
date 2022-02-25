using IdentityServer.Domain.Generic.Contracts.Responses;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState : State<SessionState>
{
    public override void Initialize()
    {
        
        
    }
    public Domain.Generic.Enums.SessionState State { get; set; }
    public List<ContactResponse> ContactList { get; set; }
    public CredentialResponse Credential { get; set; }
    public IdentityResponse Identity { get; set; }
    public SignInRequest LoginVm { get; set; }
}