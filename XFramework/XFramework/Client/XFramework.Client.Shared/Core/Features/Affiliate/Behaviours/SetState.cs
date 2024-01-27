namespace XFramework.Client.Shared.Core.Features.Affiliate;

public partial class AffiliateState
{
    public record SetState : StateAction
    {
        public bool? IsBusy { get; set; }
        public bool? NoSpinner { get; set; }
        public string ProgressTitle { get; set; }
        public string ProgressMessage { get; set; } = string.Empty;
        public bool? StateRestored { get; set; }
        public int? NotificationCount { get; set; }
    }
    
    protected class SetStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<SetState>(handlerServices, store)
    {
        private AffiliateState CurrentState => Store.GetState<AffiliateState>();

        public override async Task Handle(SetState action, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.SetProperties(action,CurrentState);
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