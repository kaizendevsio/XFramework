using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class InitiateResetPassword : NavigableRequest, IRequest<CmdResponse>
    {
    }
}