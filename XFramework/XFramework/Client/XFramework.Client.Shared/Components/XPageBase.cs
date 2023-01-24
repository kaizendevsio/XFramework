using XFramework.Client.Shared.Core.Features.Layout;

namespace XFramework.Client.Shared.Components;

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