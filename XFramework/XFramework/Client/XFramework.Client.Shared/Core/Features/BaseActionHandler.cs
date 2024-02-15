using Serilog;
using TypeSupport.Extensions;
using XFramework.Client.Shared.Core.Features.Address;
using XFramework.Client.Shared.Core.Features.Cache;
using XFramework.Client.Shared.Core.Features.Layout;
using XFramework.Client.Shared.Core.Features.Wallet;
using XFramework.Client.Shared.Entity.Enums;
using XFramework.Client.Shared.Entity.Models.Requests.Common;
using XFramework.Integration.Security;

namespace XFramework.Client.Shared.Core.Features;

public class BaseStateActionHandler
{
    public IConfiguration Configuration { get;set; }
    public IndexedDbService IndexedDbService { get; set; }
    public ISessionStorageService SessionStorageService { get; set; }
    public ILocalStorageService LocalStorageService { get; set; }
    public ISnackbar Snackbar { get; set; }
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
            if (HostEnvironment.IsProduction() || HostEnvironment.IsStaging())
            {
                SweetAlertService.FireAsync("Error", "There was an error while trying to process your request, please try again later", SweetAlertIcon.Error);
            }
            else
            {
                SweetAlertService.FireAsync("Error", message, SweetAlertIcon.Error);
            }
        }

        // Display error to the console
        Log.Error("Error from response: {Message}", message); 

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
        
        if (!silent)
        {
            if (HostEnvironment.IsProduction() || HostEnvironment.IsStaging())
            {
                switch (response.HttpStatusCode)
                {
                    case HttpStatusCode.InternalServerError:
                        SweetAlertService.FireAsync("Error",
                            "There was an error while trying to process your request, please try again later",
                            SweetAlertIcon.Error);

                        goto JumpToError;
                        break;
                }
            }
            else
            {
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $" {response.Message}"
                    : $"{customMessage}: {response.Message}", SweetAlertIcon.Error);
            }
        }

        JumpToError:

        // Display error to the console
        Log.Error("Error from response: {Message}", response.Message); 
        
        HandleFailureHooks(action);
        return true;
    }
    public async Task<bool> HandleFailure<TAction, TRequest>(CmdResponse<TRequest> response, TAction action, bool silent = false,  string customMessage = "")
    {
        if ((int)response.HttpStatusCode < 300) return false;
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});

        if (!silent)
        {
            if (HostEnvironment.IsProduction() || HostEnvironment.IsStaging())
            {
                switch (response.HttpStatusCode)
                {
                    case HttpStatusCode.InternalServerError:
                        SweetAlertService.FireAsync("Error",
                            "There was an error while trying to process your request, please try again later",
                            SweetAlertIcon.Error);

                        goto JumpToError;
                        break;
                }
            }
            else
            {
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $" {response.Message}"
                    : $"{customMessage}: {response.Message}", SweetAlertIcon.Error);
            }
        }

        JumpToError:

        // Display error to the console
        Log.Error("Error from response: {Message}", response.Message); 

        HandleFailureHooks(action);
        return true;
    }
    public async Task<bool> HandleFailure<TResponse,TAction>(QueryResponse<TResponse> response, TAction action, bool silent = false,  string customMessage = "")
    {
        if ((int)response.HttpStatusCode < 300) return false;
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});

        if (!silent)
        {
            if (HostEnvironment.IsProduction() || HostEnvironment.IsStaging())
            {
                switch (response.HttpStatusCode)
                {
                    case HttpStatusCode.InternalServerError:
                        SweetAlertService.FireAsync("Error",
                            "There was an error while trying to process your request, please try again later",
                            SweetAlertIcon.Error);

                        goto JumpToError;
                        break;
                }
            }
            else
            {
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $" {response.Message}"
                    : $"{customMessage}: {response.Message}", SweetAlertIcon.Error);
            }
        }

        JumpToError:
        
        // Display error to the console
        Log.Error("Error from response: {Message}", response.Message); 

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
        if (!silent)
        {
            SweetAlertService.FireAsync("Success", string.IsNullOrEmpty(customMessage)
                ? $"Success: {response.Message}"
                : $"{customMessage}", SweetAlertIcon.Success);
                
        }
        HandleSuccessHooks(action);
    }
    public async Task HandleSuccess<TAction, TRequest>(CmdResponse<TRequest> response, TAction action, bool silent = false, string customMessage = "")
    {
        // Display message to UI
        if (!silent)
        {
            SweetAlertService.FireAsync("Success", string.IsNullOrEmpty(customMessage)
                ? $"Success: {response.Message}"
                : $"{customMessage}", SweetAlertIcon.Success);
        }
        
        HandleSuccessHooks(action);
    }
    public async Task HandleSuccess<TResponse,TAction>(QueryResponse<TResponse> response, TAction action, bool silent = false, string customMessage = "")
    {
        // Display message to UI
        if (!silent)
        {
            SweetAlertService.FireAsync("Success", string.IsNullOrEmpty(customMessage)
                ? $"Success: {response.Message}"
                : $"{customMessage}", SweetAlertIcon.Success);
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
    
    public async Task ReportBusy(string? title = null, bool? isBusy = true)
    {
        var y =Snackbar.Add(
            message: title,
            severity: Severity.Info
        );
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = isBusy});
    }
    public async Task ReportTaskCompleted()
    {
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
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
        Snackbar = handlerServices.Snackbar;
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