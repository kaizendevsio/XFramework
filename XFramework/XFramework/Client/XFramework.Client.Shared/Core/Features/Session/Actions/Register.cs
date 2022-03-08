namespace XFramework.Client.Shared.Core.Features.Session;
public partial class SessionState
{
    public class Register : IAction
    {
        public string NavigateToOnSuccess { get; set; }
        public string NavigateToOnFailure { get; set; }
    }
}