using XFramework.Client.Shared.Entity.Enums;

namespace XFramework.Client.Shared.Core.Features.Layout;

public partial class LayoutState
{
    public record SetState : BaseAction
    {
        public LayoutIntent LayoutIntent { get; set; }
        public ViewProp View { get; set; }
    }
    
    protected class SetStateHandler(HandlerServices handlerServices, IStore store)
        : ActionHandler<SetState>(handlerServices, store)
    {
        private LayoutState CurrentState => Store.GetState<LayoutState>();

        public override async Task Handle(SetState action, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.SetProperties(action, CurrentState);
                CurrentState.LayoutIntent = action.LayoutIntent != LayoutIntent.NotSpecified
                    ? action.LayoutIntent
                    : CurrentState.LayoutIntent;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return;
        }
    }
}