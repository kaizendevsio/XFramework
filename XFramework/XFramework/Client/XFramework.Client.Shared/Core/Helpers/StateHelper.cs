using TypeSupport.Extensions;

namespace XFramework.Client.Shared.Core.Helpers;

public static class StateHelper
{
    public static void SetProperties<TAction, TState>(TAction action, TState state)
    {
        if (action is null) return;
        
        foreach (var actionProp in action.GetProperties(PropertyOptions.HasSetter))
        {
            var currentState = state.GetPropertyValue(actionProp.Name);
            var actionValue = action.GetPropertyValue(actionProp.Name);
            var finalValue = actionValue ?? currentState;
            state.SetPropertyValue(actionProp.Name, finalValue);
        }

        if (!state.ContainsProperty("InvokeRefresh")) return;
        var invokable = state.GetPropertyValue("InvokeRefresh");
        var invokableAction = (Action) invokable;
        invokableAction?.Invoke();
    }
    
    public static void ClearProperties<TAction, TState>(TAction action, TState state)
    {
        if (action is null) return;
        
        foreach (var actionProp in action.GetType().GetProperties())
        {
            var currentStateProps = state.GetType().GetProperties().Where(i => i.Name == actionProp.Name);
            var propertyInfos = currentStateProps.ToList();
            if (!propertyInfos.Any())
            {
                Console.WriteLine($"Warning: No property '{actionProp.Name}' found on '{state.GetType().Name}' state");
                continue;
            }

            var prop = propertyInfos.First();
            var currentState = prop.GetValue(state);
            var actionValue = actionProp.GetValue(action);
            var finalValue = actionValue is null ? currentState : null;

            prop.SetValue(state, finalValue);
        }
    }

    public static void RestoreState<TAction, TState>(IMediator mediator ,IndexedDbService indexedDbService , TAction action, TState state)
    {
        var s = indexedDbService.Database.StateCache.FirstOrDefault(i => i.Key == $"{state.GetType().Name}")?.Value;
        if (s is null)
        {
            mediator.Send(Activator.CreateInstance<TAction>());
            return;
        }

        SetProperties(JsonSerializer.Deserialize<TAction>(s), state);
    }
}