using BlazorState;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class NavigateToPath : IAction
    {
        public string NavigationPath { get; set; }
    }
}