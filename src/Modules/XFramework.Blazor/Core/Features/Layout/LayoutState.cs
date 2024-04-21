using XFramework.Blazor.Entity.Enums;

namespace XFramework.Blazor.Core.Features.Layout;

public partial class LayoutState : State<LayoutState>
{
    public override void Initialize()
    {
            
    }
    
    public LayoutIntent LayoutIntent { get; set; } = LayoutIntent.View;
    public ViewProp View { get; set; } = new();
}