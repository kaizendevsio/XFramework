namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class SignUpAction : IAction
    {
        public SignUpRequest Request { get; set; }
        public string NavigateTo { get; set; }
    }
}
