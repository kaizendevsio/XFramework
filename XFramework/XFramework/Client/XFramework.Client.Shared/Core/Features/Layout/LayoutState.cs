using XFramework.Client.Shared.Entity.Enums;

namespace XFramework.Client.Shared.Core.Features.Layout;

public partial class LayoutState : State<LayoutState>
{
    public override void Initialize()
    {
            
    }
    
    public LayoutIntent LayoutIntent { get; set; } = LayoutIntent.View;
    public ViewProp View { get; set; } = new();
}