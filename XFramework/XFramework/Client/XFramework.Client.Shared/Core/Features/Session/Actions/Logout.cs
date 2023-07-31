namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class Logout : BaseAction
    {
        public string NavigateTo { get; set; }
        public bool ResetAllStates { get; set; } = true;
    }
}