using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using TypeSupport.Extensions;
using XFramework.Client.Shared.Core.Features.Configuration;
using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Client.Shared.Core.Features.Cache;
using XFramework.Client.Shared.Core.Features.Layout;
using XFramework.Client.Shared.Core.Features.Session;

namespace XFramework.Client.Shared.Core.Features;

public abstract class ActionHandler<TAction> : IRequestHandler<TAction>, IRequestHandler<TAction, Unit>
    where TAction : IAction
{
    public IConfiguration Configuration { get;set; }
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
    
    protected ConfigurationState ConfigurationState => Store.GetState<ConfigurationState>();
    protected ApplicationState ApplicationState => Store.GetState<ApplicationState>();
    protected SessionState SessionState => Store.GetState<SessionState>();
    protected LayoutState LayoutState => Store.GetState<LayoutState>();
    protected CacheState CacheState => Store.GetState<CacheState>();

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
        if (response.HttpStatusCode is HttpStatusCode.Accepted) return false;
       
        // Display message to UI
        switch (silent)
        {
            case true:
                SweetAlertService.FireAsync("Error", $"There was an error while trying to process your request, please try again later");
                break;
            case false:
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $"There was an error while trying to process your request: {response.Message}"
                    : $"{customMessage}: {response.Message}");
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
        if (response.HttpStatusCode is HttpStatusCode.Accepted) return false;
        
        // Display message to UI
        switch (silent)
        {
            case true:
                SweetAlertService.FireAsync("Error", $"There was an error while trying to process your request, please try again later");
                break;
            case false:
                SweetAlertService.FireAsync("Error", string.IsNullOrEmpty(customMessage)
                    ? $"There was an error while trying to process your request: {response.Message}"
                    : $"{customMessage}: {response.Message}");
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
                    : $"{customMessage}: {response.Message}");
                break;
        }
        
        // If Success URL property is provided, navigate to the given URL
        if (action.ContainsProperty("NavigateToOnSuccess"))
        {
            var s = action.GetPropertyValue("NavigateToOnSuccess")?.ToString();
            NavigationManager.NavigateTo(s);
        }
        
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
                    : $"{customMessage}: {response.Message}");
                break;
        }
      
        // If Success URL property is provided, navigate to the given URL
        if (action.ContainsProperty("NavigateToOnSuccess"))
        {
            var s = action.GetPropertyValue("NavigateToOnSuccess")?.ToString();
            NavigationManager.NavigateTo(s);
        }
    }
}