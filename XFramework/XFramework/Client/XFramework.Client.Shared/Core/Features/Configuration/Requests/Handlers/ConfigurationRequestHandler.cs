using Blazored.LocalStorage;

namespace XFramework.Client.Shared.Core.Features.Configuration;

public class ConfigurationRequestHandler : ActionHandler<ConfigurationState.FetchConfigurationAction>
{
    private readonly IWebAssemblyHostEnvironment _hostEnvironment;
    private ConfigurationState CurrentState => Store.GetState<ConfigurationState>();
        
    public ConfigurationRequestHandler(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
    {
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
      
    public override async Task<Unit> Handle(ConfigurationState.FetchConfigurationAction aRequest, CancellationToken aCancellationToken)
    {
        var paths = new List<JsScriptPath>()
        {
            /*new(){Path = $"https://maps.googleapis.com/maps/api/js?key={googleMapApiKey}&libraries=places&v=3"}*/
        };
        await JsRuntime.InvokeVoidAsync("App.LoadJsFiles", JsonSerializer.Serialize(paths));
        
        Console.WriteLine("Configurations Applied");
        return Unit.Value;
    }
}