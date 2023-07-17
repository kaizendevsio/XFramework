using BlazorState;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class NavigateToPath : BaseAction
    {
        public string NavigationPath { get; set; }
    }
}