namespace XFramework.Client.Shared.Core.Features.Configuration;

public class ConfigurationRequestHandler : ActionHandler<ConfigurationState.FetchConfigurationAction>
{
    private readonly IWebAssemblyHostEnvironment _hostEnvironment;
    private ConfigurationState CurrentState => Store.GetState<ConfigurationState>();
        
    public ConfigurationRequestHandler(IWebAssemblyHostEnvironment hostEnvironment,ISessionStorageService sessionStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
    {
        _hostEnvironment = hostEnvironment;
        SessionStorageService = sessionStorageService;
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
        if (CurrentState.Configuration != null) return Unit.Value;
        await Mediator.Send(new ConfigurationState.SetState(){Configuration = new()});
        
        var response = await BaseHttpClient.GetFromJsonAsync<ConfigurationModel>("configurations.json", cancellationToken: aCancellationToken);
        await Mediator.Send(new ConfigurationState.SetState(){Configuration = response});
        Console.WriteLine("Configurations Loaded");
        

        switch (_hostEnvironment.Environment)
        {
            case "Production":
             
                break;
            case "Staging":
               
                break;
            case "Development":
              
                break;
        }
        
        var paths = new List<JsScriptPath>()
        {
            /*new(){Path = $"https://maps.googleapis.com/maps/api/js?key={googleMapApiKey}&libraries=places&v=3"}*/
        };
        await JsRuntime.InvokeVoidAsync("App.LoadJsFiles", JsonSerializer.Serialize(paths));
        
        Console.WriteLine("Configurations Applied");
        return Unit.Value;
    }
}