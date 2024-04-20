using XFramework.Blazor.Core.Features.Layout;

namespace XFramework.Blazor.Components;

public class XPageBase : XComponentsBase
{
    public ViewProp View { get; set; } = new();
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            await Mediator.Send(new LayoutState.SetState(){View = View});
        }
    }
}