namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class Logout : IAction
    {
        public string NavigateTo { get; set; }
        public bool ResetAllStates { get; set; } = true;
    }
}