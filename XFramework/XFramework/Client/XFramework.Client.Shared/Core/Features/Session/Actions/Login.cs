using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class Login : NavigableRequest, IRequest<CmdResponse>
    {
        public bool SkipVerification { get; set; }
        public bool InitializeWallets { get; set; }
        public bool AutoRefreshWallets { get; set; }
        public TimeSpan AutoRefreshWalletsInterval { get; set; }
        public Guid? Role { get; set; }
    }
}