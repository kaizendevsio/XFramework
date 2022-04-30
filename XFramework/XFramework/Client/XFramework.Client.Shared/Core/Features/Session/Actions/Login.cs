namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class Login : IRequest<CmdResponse>
    {
        public string NavigateToOnSuccess { get; set; }
        public string NavigateToOnFailure { get; set; }
        public string NavigateToOnVerificationRequeired { get; set; }
        public bool InitializeWallets { get; set; }
        public bool AutoRefreshWallets { get; set; }
        public TimeSpan AutoRefreshWalletsInterval { get; set; }
    }
}