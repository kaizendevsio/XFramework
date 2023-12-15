namespace XFramework.Client.Shared.Core.Features.Layout;

public partial class LayoutState
{
    public class UpdateSidebarItems : BaseAction;
    
    protected class UpdateSidebarItemsHandler(HandlerServices handlerServices, IStore store)
        : ActionHandler<UpdateSidebarItems>(handlerServices, store)
    {
            
        public override async Task Handle(UpdateSidebarItems a, CancellationToken aCancellationToken)
        {
            /*var newSidebarItems = SessionState.TenantAccountPersonaRole.Value.Select(item => new SidebarItemInfo() { Text = item.PersonaBubbleModule.BubbleModule.Name }).ToList();
            await Mediator.Send(new SetState() { SidebarInfo = new SidebarInfo() { Items = newSidebarItems } });*/

            return;
        }
        
    }
}