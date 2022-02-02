namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class ForgotPasswordAction : IAction
    {
        public ForgotPasswordRequest Request { get; set; }
        public string NavigateTo { get; set; }
    }
}
