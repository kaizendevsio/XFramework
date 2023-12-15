using XFramework.Client.Shared.Entity.Models.Requests.Wallet;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class SetState : BaseAction
    {
        public List<Domain.Generic.Contracts.Wallet>? WalletList { get; set; }
        public Domain.Generic.Contracts.Wallet? Selected { get; set; }
        public SendWalletRequest? SendWalletVm { get; set; }
        public SendWalletRequest? CurrentTransactionVm { get; set; }
    }
    
    protected class SetStateHandler(HandlerServices handlerServices, IStore store)
        : ActionHandler<SetState>(handlerServices, store)
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