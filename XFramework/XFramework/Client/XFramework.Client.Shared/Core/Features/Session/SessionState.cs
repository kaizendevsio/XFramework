
using XFramework.Domain.Generic.Contracts.Responses;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState : State<SessionState>
{
    public override void Initialize()
    {
        
    }

    public AuthenticationResponse Authentication { get; set; }
    public SignInRequest SignInRequest { get; set; } = new();
    public SignUpRequest SignUpRequest { get; set; } = new();

    public ForgotPasswordRequest ForgotPasswordRequest { get; set; } = new();
    public SmsVerificationRequest SmsVerificationRequest { get; set; } = new();
    
}