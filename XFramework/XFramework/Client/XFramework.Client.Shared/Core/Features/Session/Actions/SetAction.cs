// ReSharper disable once CheckNamespace

using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Contracts.Responses;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class SetAction : IAction
    {
        public Domain.Generic.Enums.SessionState? State { get; set; }
        public CredentialResponse Credential { get; set; }
        public IdentityResponse Identity { get; set; }
        public AuthenticationResponse Authentication { get; set; }
        public SignInRequest LoginRequest { get; set; }
        public SignUpRequest SignUpRequest { get; set; }
        public ForgotPasswordRequest ForgotPasswordRequest { get; set; }
        public SmsVerificationRequest SmsVerificationRequest { get; set; }
    }
}