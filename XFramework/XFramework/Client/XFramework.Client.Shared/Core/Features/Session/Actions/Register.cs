using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Session;
public partial class SessionState
{
    public class Register : NavigableRequest, IRequest<CmdResponse>
    {
        public bool AutoLogin { get; set; } = true;
        public bool SkipVerification { get; set; }
        public List<(Guid?, decimal)> WalletList { get; set; }
        public bool IsSilent { get; set; }
        public List<Guid?> RoleList { get; set; }
    }
}