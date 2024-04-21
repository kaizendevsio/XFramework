using System.Diagnostics;
using XFramework.Blazor.Entity.Enums;

namespace XFramework.Blazor.Core.Helpers;

public static class StateHelper
{
    public static void SetProperties<TAction, TState>(TAction action, TState state)
    {
        if (action is null) return;

        // Get the type of the action and state objects.
        var actionType = typeof(TAction);
        var stateType = typeof(TState);

        // Loop through the properties of the action object.
        foreach (var actionProp in actionType.GetProperties())
        {
            // Check if the property has a setter and skip it if it doesn't.
            if (!actionProp.CanWrite) continue;

            // Get the current value of the property in the state object.
            var stateProp = stateType.GetProperty(actionProp.Name);
            var currentState = stateProp?.GetValue(state);

            // Get the value of the property in the action object.
            var actionValue = actionProp.GetValue(action);

            // Choose the final value for the property by using the action value if it is not null, otherwise use the current state value.
            var finalValue = actionValue ?? currentState;

            // Set the property value in the state object.
            stateProp?.SetValue(state, finalValue);
        }

        // Check if the state object has a property named "InvokeRefresh" and skip it if it doesn't.
        var invokeRefreshProp = stateType.GetProperty("InvokeRefresh");
        if (invokeRefreshProp == null) return;

        // Get the value of the "InvokeRefresh" property in the state object.
        var invokable = invokeRefreshProp.GetValue(state);

        // Check if the value is of type Action and skip it if it isn't.
        if (invokable is not Action invokableAction) return;

        // Invoke the action.
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

    public static async Task RestoreState<TAction, TState>(IMediator mediator, IndexedDbService indexedDbService, ISessionStorageService sessionStorageService, 
        ILocalStorageService localStorageService, TAction action, TState state, PersistStateBy persistStateBy = PersistStateBy.SessionStorage)
    {
        // Get the type of the state object.
    var stateType = typeof(TState);

    // Initialize the "s" variable with the default value of null.
    var s = default(string);

    // Use a switch statement to choose the value of the "s" variable based on the value of the "persistStateBy" parameter.
    switch (persistStateBy)
    {
        case PersistStateBy.NotSpecified:
            throw new NotImplementedException($"State persistence by '{nameof(persistStateBy)}' is not yet implemented");
        case PersistStateBy.LocalStorage:
            s = await localStorageService.GetItemAsStringAsync(stateType.Name, CancellationToken.None);
            break;
        case PersistStateBy.SessionStorage:
            s = await sessionStorageService.GetItemAsStringAsync(stateType.Name, CancellationToken.None);
            break;
        case PersistStateBy.IndexDb:
            var stateCache = indexedDbService.Database.StateCache;
            s = stateCache.FirstOrDefault(i => i.Key == stateType.Name)?.Value;
            break;
        case PersistStateBy.CloudStore:
            throw new NotImplementedException($"State persistence by '{nameof(persistStateBy)}' is not yet implemented");
        case PersistStateBy.GoogleDrive:
            throw new NotImplementedException($"State persistence by '{nameof(persistStateBy)}' is not yet implemented");
        case PersistStateBy.OneDrive:
            throw new NotImplementedException($"State persistence by '{nameof(persistStateBy)}' is not yet implemented");
        default:
            throw new ArgumentOutOfRangeException(nameof(persistStateBy), persistStateBy, null);
    }

    // Check if the "s" variable is null and return if it is.
    if (s is null)
    { 
        mediator.Send(Activator.CreateInstance<TAction>());
        return;
    }

    // Deserialize the "s" variable to a TAction object.
    var actionObject = JsonSerializer.Deserialize<TAction>(s);

    // Measure the time it takes to restore the state.
    var stopwatch = new Stopwatch();
    stopwatch.Start();

    // Use the SetProperties method to restore the state from the action object.
    SetProperties(actionObject, state);

    // Stop the stopwatch and log the elapsed time.
    stopwatch.Stop();
    Console.WriteLine($"Restore {stateType.Name} state took {stopwatch.ElapsedMilliseconds}ms");
    }
}