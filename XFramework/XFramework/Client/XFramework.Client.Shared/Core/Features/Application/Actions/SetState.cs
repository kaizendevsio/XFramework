namespace XFramework.Client.Shared.Core.Features.Application;

public partial class ApplicationState
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
        private ApplicationState CurrentState => Store.GetState<ApplicationState>();

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