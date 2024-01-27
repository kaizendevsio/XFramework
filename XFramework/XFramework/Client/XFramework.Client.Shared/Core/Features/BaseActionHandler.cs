using TypeSupport.Extensions;
using XFramework.Client.Shared.Core.Features.Address;
using XFramework.Client.Shared.Core.Features.Cache;
using XFramework.Client.Shared.Core.Features.Layout;
using XFramework.Client.Shared.Core.Features.Wallet;
using XFramework.Client.Shared.Entity.Enums;
using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features;

public class BaseStateActionHandler
{
    public IConfiguration Configuration { get;set; }
    public IndexedDbService IndexedDbService { get; set; }
    public ISessionStorageService SessionStorageService { get; set; }
    public ILocalStorageService LocalStorageService { get; set; }
    protected SweetAlertService SweetAlertService { get; set; }
    protected NavigationManager NavigationManager { get; set; }
    protected IWebAssemblyHostEnvironment HostEnvironment { get; set; }
    protected IHttpClient HttpClient { get; set; }
    protected HttpClient BaseHttpClient { get; set; }
    protected IJSRuntime JsRuntime { get; set; }
    protected IMediator Mediator { get; set; }
    protected EndPointsModel EndPoints { get; set; }
    protected IStore Store { get; set; }
    public bool IsSilent { get; set; }
    
    protected ApplicationState ApplicationState => Store.GetState<ApplicationState>();
    protected AddressState AddressState => Store.GetState<AddressState>();
    protected SessionState SessionState => Store.GetState<SessionState>();
    protected LayoutState LayoutState => Store.GetState<LayoutState>();
    protected CacheState CacheState => Store.GetState<CacheState>();
    protected WalletState WalletState => Store.GetState<WalletState>();
    
    public async Task<CmdResponse> HandleFailure<TAction>(TAction action, string message, bool silent = false)
    {
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
       
        // Display message to UI
        if (!silent)
        {
            SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(message)
                ? $"There was an error while trying to process your request, please try again later"
                : $"{message}", SweetAlertIcon.Error);
        }
        
        // Display error to the console
        Console.WriteLine($"Error from response: {message}");

        HandleFailureHooks(action);
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.InternalServerError,
            Message = message
        };
    }
    public async Task<bool> HandleFailure<TAction>(CmdResponse response, TAction action, bool silent = false,  string customMessage = "")
    {
        if ((int)response.HttpStatusCode < 300) return false;
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
        
        if (HostEnvironment.IsProduction() || HostEnvironment.IsStaging())
        {
            switch (response.HttpStatusCode)
            {
                case HttpStatusCode.InternalServerError:
                    SweetAlertService.FireAsync("Error", "There was an error while trying to process your request, please try again later", SweetAlertIcon.Error);
                    
                    goto JumpToError;
                    break;
            }

        }
        
        // Display message to UI
        switch (silent)
        {
            case true:
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $"There was an error while trying to process your request, please try again later"
                    : $"{customMessage}", SweetAlertIcon.Error);
                break;
            case false:
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $"There was an error while trying to process your request: {response.Message}"
                    : $"{customMessage}: {response.Message}", SweetAlertIcon.Error);
                break;
        }

        JumpToError:

        // Display error to the console
        Console.WriteLine($"Error from response: {response.Message}");

        HandleFailureHooks(action);
        return true;
    }
    public async Task<bool> HandleFailure<TAction, TRequest>(CmdResponse<TRequest> response, TAction action, bool silent = false,  string customMessage = "")
    {
        if ((int)response.HttpStatusCode < 300) return false;
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
        
        if (HostEnvironment.IsProduction() || HostEnvironment.IsStaging())
        {
            switch (response.HttpStatusCode)
            {
                case HttpStatusCode.InternalServerError:
                    SweetAlertService.FireAsync("Error", "There was an error while trying to process your request, please try again later", SweetAlertIcon.Error);
                    
                    goto JumpToError;
                    break;
            }

        }
        
        // Display message to UI
        switch (silent)
        {
            case true:
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $"There was an error while trying to process your request, please try again later"
                    : $"{customMessage}", SweetAlertIcon.Error);
                break;
            case false:
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $"There was an error while trying to process your request: {response.Message}"
                    : $"{customMessage}: {response.Message}", SweetAlertIcon.Error);
                break;
        }

        JumpToError:

        // Display error to the console
        Console.WriteLine($"Error from response: {response.Message}");

        HandleFailureHooks(action);
        return true;
    }
    public async Task<bool> HandleFailure<TResponse,TAction>(QueryResponse<TResponse> response, TAction action, bool silent = false,  string customMessage = "")
    {
        if ((int)response.HttpStatusCode < 300) return false;
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});

        if (HostEnvironment.IsProduction() || HostEnvironment.IsStaging())
        {
            switch (response.HttpStatusCode)
            {
                case HttpStatusCode.InternalServerError:
                    SweetAlertService.FireAsync("Error", "There was an error while trying to process your request, please try again later", SweetAlertIcon.Error);
                    
                    goto JumpToError;
                    break;
            }

        }
        
        // Display message to UI
        switch (silent)
        {
            case true:
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $"There was an error while trying to process your request, please try again later"
                    : $"{customMessage}", SweetAlertIcon.Error);
                break;
            case false:
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $"There was an error while trying to process your request: {response.Message}"
                    : $"{customMessage}", SweetAlertIcon.Error);
                break;
        }

        JumpToError:
        // Display error to the console
        Console.WriteLine($"Error from response: {response.Message}");

        HandleFailureHooks(action);
        return true;
    }
    public async Task<CmdResponse> HandleSuccess<TAction>(TAction action, string message, bool silent = true)
    {
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
        
        // Display message to UI
        if (!silent)
        {
            SweetAlertService.FireAsync("Success", string.IsNullOrEmpty(message)
                ? null
                : $"{message}", SweetAlertIcon.Success);
        }

        HandleSuccessHooks(action);
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = message
        };
    }
    public async Task HandleSuccess<TAction>(CmdResponse response, TAction action, bool silent = false, string customMessage = "")
    {
        // Display message to UI
        switch (silent)
        {
            case true:
                break;
            case false:
                  SweetAlertService.FireAsync("Success", string.IsNullOrEmpty(customMessage)
                    ? $"Success: {response.Message}"
                    : $"{customMessage}", SweetAlertIcon.Success);
                break;
        }
        HandleSuccessHooks(action);
    }
    public async Task HandleSuccess<TAction, TRequest>(CmdResponse<TRequest> response, TAction action, bool silent = false, string customMessage = "")
    {
        // Display message to UI
        switch (silent)
        {
            case true:
                break;
            case false:
                SweetAlertService.FireAsync("Success", string.IsNullOrEmpty(customMessage)
                    ? $"Success: {response.Message}"
                    : $"{customMessage}", SweetAlertIcon.Success);
                break;
        }
        
        HandleSuccessHooks(action);
    }
    public async Task HandleSuccess<TResponse,TAction>(QueryResponse<TResponse> response, TAction action, bool silent = false, string customMessage = "")
    {
        // Display message to UI
        switch (silent)
        {
            case true:
                break;
            case false:
                SweetAlertService.FireAsync("Success", string.IsNullOrEmpty(customMessage)
                    ? $"Success: {response.Message}"
                    : $"{customMessage}", SweetAlertIcon.Success);
                break;
        }
      
        HandleSuccessHooks(action);
    }

    private void HandleSuccessHooks<TAction>(TAction action)
    {
        if (action.ContainsProperty("NavigateToOnSuccess"))
        {
            var s = action.GetPropertyValue("NavigateToOnSuccess");
            if (s is not null)
            {
                NavigationManager.NavigateTo(s.ToString());
            }
        }
        
        var onSuccess = action.GetPropertyValue("OnSuccess");
        if (onSuccess is not null)
        {
            var onSuccessAction = onSuccess as Action;
            onSuccessAction?.Invoke();
        }
        
        ReportTaskCompleted();
    }
    private void HandleFailureHooks<TAction>(TAction action)
    {
        if (action.ContainsProperty("NavigateToOnFailure"))
        {
            var s = action.GetPropertyValue("NavigateToOnFailure");
            if (s is not null)
            {
                NavigationManager.NavigateTo(s.ToString());
            }
        }
        
        var onFailure = action.GetPropertyValue("OnFailure");
        if (onFailure is not null)
        {
            var failureAction = onFailure as Action;
            failureAction?.Invoke();
        }
        
        ReportTaskCompleted();
    }
    
    public async Task Persist<TState>(TState state)
    {
        var statePersistenceFromAppSettings = Configuration.GetValue<string>("Application:Persistence:State:Driver");
        var persistStateBy = (PersistStateBy)Enum.Parse(typeof(PersistStateBy), statePersistenceFromAppSettings);
        
        switch (persistStateBy)
        {
            case PersistStateBy.NotSpecified:
                throw new NotImplementedException($"State persistence by '{nameof(persistStateBy)}' is not yet implemented");
            case PersistStateBy.LocalStorage:
                await LocalStorageService.SetItemAsync(state.GetType().Name, state);
                break;
            case PersistStateBy.SessionStorage:
                await SessionStorageService.SetItemAsync(state.GetType().Name, state);
                break;
            case PersistStateBy.IndexDb:
                #region Implementation

                if (IndexedDbService is null) Console.WriteLine("IndexedDbService is not initialized!");
                if (IndexedDbService.Database is null)
                {
                    if (IndexedDbService.IsInitializing)
                    {
                        await IndexedDbService.TaskCompletionSource.Task;
                        goto ResumeTask;
                    }
                    await IndexedDbService.InitializeDb();
                };
        
                ResumeTask:
                var stateName = state.GetType().Name;
                var stateEntry = IndexedDbService.Database.StateCache.FirstOrDefault(i => i.Key == stateName);
                var stateValue = JsonSerializer.Serialize(state);

                if (stateEntry is null)
                {
                    //IndexedDbService.Database.StateCache.Clear();
                    await IndexedDbService.InitializeDb();
                    IndexedDbService.Database.StateCache.Add(new()
                    {
                        Key = stateName,
                        Value = stateValue
                        //Signature = stateValue.ToMd5()
                    });
                    Console.WriteLine($"'{stateName}' State Added To Indexed DB ");
                }
                else
                {
                    stateEntry.Value = stateValue;
                    //stateEntry.Signature = stateValue.ToMd5();
                    Console.WriteLine($"'{stateName}' State Updated To Indexed DB ");
                }
        
                await IndexedDbService.Database.SaveChanges();
                await IndexedDbService.InitializeDb();

                #endregion
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
    }
    
    public Task DisplayProgress(bool show)
    {
        if (show)
        {
            SweetAlertService.FireAsync(new()
            {
                Backdrop = false,
                Html = $"<div class='loadingio-spinner-ellipsis-hm5jphe6my'><div class='ldio-o8ctnog1lcq'><div></div><div></div><div></div><div></div><div></div></div></div>",
                ShowConfirmButton = false,
            });
            return Task.CompletedTask;
        }
        SweetAlertService.CloseAsync();
        return Task.CompletedTask;
    }
    
    public async Task ReportTask(string title, bool? isBusy = true)
    {
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = isBusy, ProgressMessage = title, NoSpinner = false});
    }
    public async Task ReportTask<T>(QueryAction action)
    {
        if (action.Silent) { await Mediator.Send(new ApplicationState.SetState() {IsBusy = true, NoSpinner = true, ProgressTitle = action.GetType().Name}); return;}
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = true, ProgressTitle = action.GetType().Name, NoSpinner = false});
    }
    public async Task ReportTask<T,TQ>(QueryAction action, IEnumerable<T> list)
    {
        if (action.Silent) { await Mediator.Send(new ApplicationState.SetState() {IsBusy = true, NoSpinner = true, ProgressTitle = action.GetType().Name}); return;}
        if (list.TryGetNonEnumeratedCount(out var count) && count > 0) return;
        if (list.Any()) return;
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = true, ProgressTitle = action.GetType().Name, NoSpinner = false});
    }
    public async Task ReportTaskCompleted()
    {
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
    }
    
    public async Task ReportTaskCompleted(QueryAction action)
    {
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
    }
    public async Task ReportTaskCompleted(NavigableRequest action)
    {
        if (!action.Silent)
        {
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
        }
    }
    
    public async Task NavigateTo(string path)
    {
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
        NavigationManager.NavigateTo(path);
    }
}

public abstract class StateActionHandler<TAction> : BaseStateActionHandler, IRequestHandler<TAction>
    where TAction : IAction
{
    protected StateActionHandler(HandlerServices handlerServices, IStore store)
    {
        Configuration = handlerServices.Configuration;
        SessionStorageService = handlerServices.SessionStorageService;
        LocalStorageService = handlerServices.LocalStorageService;
        SweetAlertService = handlerServices.SweetAlertService;
        NavigationManager = handlerServices.NavigationManager;
        EndPoints = handlerServices.EndPoints;
        HttpClient = handlerServices.HttpClient;
        HostEnvironment = handlerServices.HostEnvironment;
        BaseHttpClient = handlerServices.BaseHttpClient;
        JsRuntime = handlerServices.JsRuntime;
        Mediator = handlerServices.Mediator;
        Store = store;
    }
    public abstract Task Handle(TAction action, CancellationToken aCancellationToken);
}

public abstract class EventHandler<TAction> : BaseStateActionHandler, INotificationHandler<TAction>
    where TAction : INotification
{
    protected EventHandler(HandlerServices handlerServices, IStore store)
    {
        Configuration = handlerServices.Configuration;
        SessionStorageService = handlerServices.SessionStorageService;
        LocalStorageService = handlerServices.LocalStorageService;
        SweetAlertService = handlerServices.SweetAlertService;
        NavigationManager = handlerServices.NavigationManager;
        EndPoints = handlerServices.EndPoints;
        HttpClient = handlerServices.HttpClient;
        HostEnvironment = handlerServices.HostEnvironment;
        BaseHttpClient = handlerServices.BaseHttpClient;
        JsRuntime = handlerServices.JsRuntime;
        Mediator = handlerServices.Mediator;
        Store = store;
    }
    public abstract Task Handle(TAction action, CancellationToken aCancellationToken);

}

public abstract class StateActionHandler<TAction, TResponse> : BaseStateActionHandler, IRequestHandler<TAction, TResponse> 
    where TAction : IRequest<TResponse>
{
    protected StateActionHandler(HandlerServices handlerServices, IStore store)
    {
        Configuration = handlerServices.Configuration;
        SessionStorageService = handlerServices.SessionStorageService;
        LocalStorageService = handlerServices.LocalStorageService;
        SweetAlertService = handlerServices.SweetAlertService;
        NavigationManager = handlerServices.NavigationManager;
        EndPoints = handlerServices.EndPoints;
        HttpClient = handlerServices.HttpClient;
        HostEnvironment = handlerServices.HostEnvironment;
        BaseHttpClient = handlerServices.BaseHttpClient;
        JsRuntime = handlerServices.JsRuntime;
        Mediator = handlerServices.Mediator;
        Store = store;
    }
    public abstract Task<TResponse> Handle(TAction action, CancellationToken aCancellationToken);
}