﻿namespace XFramework.Blazor.Core.Features;

public abstract class ClearStateHandler<TAction, TState>(HandlerServices handlerServices, IStore store)
    : StateActionHandler<TAction>(handlerServices, store) where TAction : IAction
{
    public TState CurrentState => Store.GetState<TState>();
        
    public override async Task Handle(TAction state, CancellationToken aCancellationToken)
    {
        try
        {
            StateHelper.ClearProperties(state, CurrentState);
            Persist(CurrentState);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}