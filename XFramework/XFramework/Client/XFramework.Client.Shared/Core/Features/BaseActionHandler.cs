using XFramework.Client.Shared.Core.Features.Configuration;
using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Client.Shared.Core.Features.Cache;
using XFramework.Client.Shared.Core.Features.Layout;
using XFramework.Client.Shared.Core.Features.Session;

namespace XFramework.Client.Shared.Core.Features;

public abstract class ActionHandler<TAction> : IRequestHandler<TAction>, IRequestHandler<TAction, Unit>
    where TAction : IAction
{
    protected ISessionStorageService SessionStorageService { get; set; }
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

    protected ActionHandler(ISessionStorageService sessionStorageService, SweetAlertService sweetAlertService,
        NavigationManager navigationManager, EndPointsModel endPointsModel, IHttpClient httpClient,
        HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store)
    {
        SessionStorageService = sessionStorageService;
        SweetAlertService = sweetAlertService;
        NavigationManager = navigationManager;
        HttpClient = httpClient;
        BaseHttpClient = baseHttpClient;
        JsRuntime = jsRuntime;
        Mediator = mediator;
        Store = store;
    }
    public abstract Task<Unit> Handle(TAction action, CancellationToken aCancellationToken);
}