
using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Contracts.Responses;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState : State<SessionState>
{
    public override void Initialize()
    {
        State  = Domain.Generic.Enums.SessionState.Inactive;
    }
    public Domain.Generic.Enums.SessionState State { get; set; }

    public CredentialResponse Credential { get; set; }
    public IdentityResponse Identity { get; set; }
    
    public AuthenticationResponse Authentication { get; set; }
    public SignInRequest SignInRequest { get; set; } = new();   
    public SignUpRequest SignUpRequest { get; set; } = new();

    public ForgotPasswordRequest ForgotPasswordRequest { get; set; } = new();
    public SmsVerificationRequest SmsVerificationRequest { get; set; } = new();
    
}