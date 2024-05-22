using XFramework.Blazor.Entity.Models.Requests.Wallet;

namespace XFramework.Blazor.Core.Features.Wallet;

public partial class WalletState
{
    public record SetState : StateAction
    {
        public List<Domain.Shared.Contracts.Wallet>? WalletList { get; set; }
        public Domain.Shared.Contracts.Wallet? Selected { get; set; }
        public SendWalletRequest? SendWalletVm { get; set; }
        public SendWalletRequest? CurrentTransactionVm { get; set; }
    }
    
    protected class SetStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<SetState>(handlerServices, store)
    {
        private WalletState CurrentState => Store.GetState<WalletState>();
      
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