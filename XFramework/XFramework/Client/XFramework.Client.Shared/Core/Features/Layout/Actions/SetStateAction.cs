using XFramework.Client.Shared.Entity.Enums;

namespace XFramework.Client.Shared.Core.Features.Layout;

public partial class LayoutState
{
    public class SetState : IAction
    {
        public LayoutIntent LayoutIntent { get; set; }
        public ViewProp View { get; set; }
    }
}