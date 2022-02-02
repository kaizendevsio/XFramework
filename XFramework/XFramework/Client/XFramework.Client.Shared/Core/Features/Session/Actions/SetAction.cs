

// ReSharper disable once CheckNamespace
namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class SetAction : IAction
    {
        public AuthenticationResponse Authentication { get; set; }
        public SignInRequest LoginRequest { get; set; }
        public SignUpRequest SignUpRequest { get; set; }
        public ForgotPasswordRequest ForgotPasswordRequest { get; set; }
        public SmsVerificationRequest SmsVerificationRequest { get; set; }
    }
}