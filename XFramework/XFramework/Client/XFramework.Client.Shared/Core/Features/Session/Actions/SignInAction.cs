namespace XFramework.Client.Shared.Core.Features.Session;
public partial class SessionState
{
    public class SignInAction : IAction
    {
        public SignInRequest Request { get; set; }
        public string NavigateTo { get; set; }
    }
}