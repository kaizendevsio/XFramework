namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState : State<SessionState>
{
    public override void Initialize()
    {
        
        
    }

    public SignInRequest LoginVm { get; set; }
    public SignUpRequest RegisterVm { get; set; }
    public ForgotPasswordRequest ForgotPasswordVm { get; set; }

}