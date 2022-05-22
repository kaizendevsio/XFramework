namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class SendVerificationCode : IRequest<CmdResponse>
    {
        public string NavigateToOnSuccess { get; set; }
        public string NavigateToOnFailure { get; set; }
    }
}