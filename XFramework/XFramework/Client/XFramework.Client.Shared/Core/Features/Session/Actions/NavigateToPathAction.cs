using BlazorState;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class NavigateToPathAction : IAction
    {
        public string NavigationPath { get; set; }
    }
}