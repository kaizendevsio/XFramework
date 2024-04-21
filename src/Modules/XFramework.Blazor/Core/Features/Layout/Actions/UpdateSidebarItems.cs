namespace XFramework.Blazor.Core.Features.Layout;

public partial class LayoutState
{
    public record UpdateSidebarItems : StateAction;
    
    protected class UpdateSidebarItemsHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<UpdateSidebarItems>(handlerServices, store)
    {
            
        public override async Task Handle(UpdateSidebarItems a, CancellationToken aCancellationToken)
        {
            /*var newSidebarItems = SessionState.TenantAccountPersonaRole.Value.Select(item => new SidebarItemInfo() { Text = item.PersonaBubbleModule.BubbleModule.Name }).ToList();
            await Mediator.Send(new SetState() { SidebarInfo = new SidebarInfo() { Items = newSidebarItems } });*/

            return;
        }
        
    }
}