namespace XFramework.Client.Shared.Core.Features;

public class SetStateHandler<TAction, TState>(HandlerServices handlerServices, IStore store) : StateActionHandler<TAction>(handlerServices, store) where TAction : IAction
{
    private TState CurrentState => Store.GetState<TState>();

    public override async Task Handle(TAction action, CancellationToken aCancellationToken)
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
    }
}