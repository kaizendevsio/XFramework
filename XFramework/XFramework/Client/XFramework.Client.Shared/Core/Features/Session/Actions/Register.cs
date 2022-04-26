namespace XFramework.Client.Shared.Core.Features.Session;
public partial class SessionState
{
    public class Register : IRequest<CmdResponse>
    {
        public bool AutoLogin { get; set; } = true;
        public List<(Guid?, decimal)> WalletList { get; set; }
        public string NavigateToOnSuccess { get; set; }
        public string NavigateToOnFailure { get; set; }
    }
}