using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Responses;

namespace XFramework.Blazor.Core.Features.Tenant;

public partial class TenantState
{
    public record SetState : StateAction
    {
        public Domain.Shared.Contracts.Tenant? SelectedTenant { get; set; }
    }
    
    protected class SetStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<SetState>(handlerServices, store)
    {
        private TenantState CurrentState => Store.GetState<TenantState>();
        
        public override async Task Handle(SetState state, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.SetProperties(state, CurrentState);
                Persist(CurrentState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return;
        }
    }
} 
