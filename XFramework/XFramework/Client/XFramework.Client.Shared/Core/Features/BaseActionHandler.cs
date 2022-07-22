using TypeSupport.Extensions;
using XFramework.Client.Shared.Core.Features.Configuration;
using XFramework.Client.Shared.Core.Features.Cache;
using XFramework.Client.Shared.Core.Features.Community;
using XFramework.Client.Shared.Core.Features.Layout;
using XFramework.Client.Shared.Core.Features.Session;
using XFramework.Client.Shared.Core.Features.Todo;
using XFramework.Client.Shared.Core.Features.Wallet;
using XFramework.Client.Shared.Core.Services;
using XFramework.Client.Shared.Entity.Enums;
using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Integration.Security;

namespace XFramework.Client.Shared.Core.Features;

public abstract class ActionHandler<TAction> : IRequestHandler<TAction>, IRequestHandler<TAction, Unit>
    where TAction : IAction
{
    public IConfiguration Configuration { get;set; }
    public IndexedDbService IndexedDbService { get; set; }
    protected ISessionStorageService SessionStorageService { get; set; }
    public ILocalStorageService LocalStorageService { get; set; }
    protected SweetAlertService SweetAlertService { get; set; }
    protected NavigationManager NavigationManager { get; set; }
    protected IHttpClient HttpClient { get; set; }
    protected HttpClient BaseHttpClient { get; set; }
    protected IJSRuntime JsRuntime { get; set; }
    protected IMediator Mediator { get; set; }
    protected EndPointsModel EndPoints { get; set; }
    protected IStore Store { get; set; }
    public bool IsSilent { get; set; }

    
    protected ConfigurationState ConfigurationState => Store.GetState<ConfigurationState>();
    protected ApplicationState ApplicationState => Store.GetState<ApplicationState>();
    protected SessionState SessionState => Store.GetState<SessionState>();
    protected LayoutState LayoutState => Store.GetState<LayoutState>();
    protected CacheState CacheState => Store.GetState<CacheState>();
    protected WalletState WalletState => Store.GetState<WalletState>();
    protected CommunityState CommunityState => Store.GetState<CommunityState>();
    protected TodoState TodoState => Store.GetState<TodoState>();

    protected ActionHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService,
        NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient,
        HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store)
    {
        Configuration = configuration;
        SessionStorageService = sessionStorageService;
        LocalStorageService = localStorageService;
        SweetAlertService = sweetAlertService;
        NavigationManager = navigationManager;
        EndPoints = endPoints;
        HttpClient = httpClient;
        BaseHttpClient = baseHttpClient;
        JsRuntime = jsRuntime;
        Mediator = mediator;
        Store = store;
    }
    public abstract Task<Unit> Handle(TAction action, CancellationToken aCancellationToken);
    public async Task<bool> HandleFailure<TAction>(CmdResponse response, TAction action, bool silent = false,  string customMessage = "")
    {
        if ((int)response.HttpStatusCode < 300) return false;
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
        
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

        // Display error to the console
        Console.WriteLine($"Error from response: {response.Message}");

        // If Fail URL property is provided, navigate to the given URL
        if (!action.ContainsProperty("NavigateToOnFailure")) return true;
        
        var s = action.GetPropertyValue("NavigateToOnFailure");
        if (s is null) return true;
        NavigationManager.NavigateTo(s.ToString());

        return true;
    }
    public async Task<bool> HandleFailure<TResponse,TAction>(QueryResponse<TResponse> response, TAction action, bool silent = false,  string customMessage = "")
    {
        if ((int)response.HttpStatusCode < 300) return false;
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
        
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

        // Display error to the console
        Console.WriteLine($"Error from response: {response.Message}");

        // If Fail URL property is provided, navigate to the given URL
        if (!action.ContainsProperty("NavigateToOnFailure")) return true;

        var s = action.GetPropertyValue("NavigateToOnFailure");
        if (s is null) return true;
        NavigationManager.NavigateTo(s.ToString());
        return false;
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
                    : $"{customMessage}" , SweetAlertIcon.Success);
                break;
        }
        
        // If Success URL property is provided, navigate to the given URL
        if (!action.ContainsProperty("NavigateToOnSuccess")) return;
        var s = action.GetPropertyValue("NavigateToOnSuccess");
        if (s is null) return;
        NavigationManager.NavigateTo(s.ToString());

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
                    : $"{customMessage}" , SweetAlertIcon.Success);
                break;
        }
      
         // If Success URL property is provided, navigate to the given URL
        if (!action.ContainsProperty("NavigateToOnSuccess")) return;
        var s = action.GetPropertyValue("NavigateToOnSuccess");
        if (s is null) return;
        NavigationManager.NavigateTo(s.ToString());
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
    public async Task ReportTask(string title, bool? isBusy = null)
    {
        if (!IsSilent)
        {
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = isBusy, ProgressTitle = title});
        }
    }
    public async Task ReportTask(QueryableRequest action, bool isDone = false)
    {
        if (action.Silent) return;
        if (!isDone)
        {
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = true});
            return;
        }
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
    }
    public async Task ReportTask<T>(QueryableRequest action, IEnumerable<T> list,bool isDone = false)
    {
        if (action.Silent) return;
        if (list.TryGetNonEnumeratedCount(out var count) && count > 0) return;
        if (list.Any()) return;
        if (!isDone)
        {
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = true});
            return;
        }
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
    }
    public async Task ReportProgress(string message)
    {
        await Mediator.Send(new ApplicationState.SetState() {ProgressMessage = message});
    }
    public async Task NavigateTo(string path)
    {
        await Mediator.Send(new SessionState.NavigateToPath() {NavigationPath = path});
    }
    
}

public abstract class ActionHandler<TAction, TResponse> : IRequestHandler<TAction, TResponse>
    where TAction : IRequest<TResponse>
{
    public IConfiguration Configuration { get;set; }
    public IndexedDbService IndexedDbService { get; set; }
    public ISessionStorageService SessionStorageService { get; set; }
    public ILocalStorageService LocalStorageService { get; set; }
    protected SweetAlertService SweetAlertService { get; set; }
    protected NavigationManager NavigationManager { get; set; }
    protected IHttpClient HttpClient { get; set; }
    protected HttpClient BaseHttpClient { get; set; }
    protected IJSRuntime JsRuntime { get; set; }
    protected IMediator Mediator { get; set; }
    protected EndPointsModel EndPoints { get; set; }
    protected IStore Store { get; set; }
    public bool IsSilent { get; set; }
    
    protected ConfigurationState ConfigurationState => Store.GetState<ConfigurationState>();
    protected ApplicationState ApplicationState => Store.GetState<ApplicationState>();
    protected SessionState SessionState => Store.GetState<SessionState>();
    protected LayoutState LayoutState => Store.GetState<LayoutState>();
    protected CacheState CacheState => Store.GetState<CacheState>();
    protected WalletState WalletState => Store.GetState<WalletState>();

    protected ActionHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService,
        NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient,
        HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store)
    {
        Configuration = configuration;
        SessionStorageService = sessionStorageService;
        LocalStorageService = localStorageService;
        SweetAlertService = sweetAlertService;
        NavigationManager = navigationManager;
        EndPoints = endPoints;
        HttpClient = httpClient;
        BaseHttpClient = baseHttpClient;
        JsRuntime = jsRuntime;
        Mediator = mediator;
        Store = store;
    }
    public abstract Task<TResponse> Handle(TAction action, CancellationToken aCancellationToken);
    public async Task<bool> HandleFailure<TAction>(CmdResponse response, TAction action, bool silent = false,  string customMessage = "")
    {
        if ((int)response.HttpStatusCode < 300) return false;
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
        
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

        // Display error to the console
        Console.WriteLine($"Error from response: {response.Message}");

        // If Fail URL property is provided, navigate to the given URL
        if (!action.ContainsProperty("NavigateToOnFailure")) return true;
        
        var s = action.GetPropertyValue("NavigateToOnFailure");
        if (s is null) return true;
        NavigationManager.NavigateTo(s.ToString());

        return true;
    }
    public async Task<bool> HandleFailure<TResponse,TAction>(QueryResponse<TResponse> response, TAction action, bool silent = false,  string customMessage = "")
    {
        if ((int)response.HttpStatusCode < 300) return false;
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
        
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

        // Display error to the console
        Console.WriteLine($"Error from response: {response.Message}");

        // If Fail URL property is provided, navigate to the given URL
        if (!action.ContainsProperty("NavigateToOnFailure")) return true;

        var s = action.GetPropertyValue("NavigateToOnFailure");
        if (s is null) return true;
        NavigationManager.NavigateTo(s.ToString());
        return false;
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
        
        // If Success URL property is provided, navigate to the given URL
        if (!action.ContainsProperty("NavigateToOnSuccess")) return;
        var s = action.GetPropertyValue("NavigateToOnSuccess");
        if (s is null) return;
        NavigationManager.NavigateTo(s.ToString());

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
      
         // If Success URL property is provided, navigate to the given URL
        if (!action.ContainsProperty("NavigateToOnSuccess")) return;
        var s = action.GetPropertyValue("NavigateToOnSuccess");
        if (s is null) return;
        NavigationManager.NavigateTo(s.ToString());
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
    public async Task ReportTask(string title, bool? isBusy = null)
    {
        if (!IsSilent)
        {
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = isBusy, ProgressTitle = title});
        }
    }
    public async Task ReportTask(QueryableRequest action, bool isDone = false)
    {
        if (action.Silent) return;
        if (!isDone)
        {
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = true});
            return;
        }
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
    }
    public async Task ReportTask<T>(QueryableRequest action, IEnumerable<T> list,bool isDone = false)
    {
        if (action.Silent) return;
        if (list.TryGetNonEnumeratedCount(out var count) && count > 0) return;
        if (list.Any()) return;
        if (!isDone)
        {
            await Mediator.Send(new ApplicationState.SetState() {IsBusy = true});
            return;
        }
        await Mediator.Send(new ApplicationState.SetState() {IsBusy = false});
    }
    public async Task ReportProgress(string message)
    {
        await Mediator.Send(new ApplicationState.SetState() {ProgressMessage = message});
    }
    
    public async Task NavigateTo(string path)
    {
        await Mediator.Send(new SessionState.NavigateToPath() {NavigationPath = path});
    }
    
}