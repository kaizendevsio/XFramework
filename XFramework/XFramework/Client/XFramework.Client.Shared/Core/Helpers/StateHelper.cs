namespace XFramework.Client.Shared.Core.Helpers;

public static class StateHelper
{
    public static void SetProperties<TAction, TState>(TAction action, TState state)
    {
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
            var finalValue = actionValue ?? currentState;

            prop.SetValue(state, finalValue);
        }
    }
    
    public static void ClearProperties<TAction, TState>(TAction action, TState state)
    {
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
}