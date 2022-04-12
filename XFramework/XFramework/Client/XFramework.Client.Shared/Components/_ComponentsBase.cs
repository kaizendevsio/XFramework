using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Client.Shared.Core.Features.Cache;
using XFramework.Client.Shared.Core.Features.Cryptocurrency;
using XFramework.Client.Shared.Core.Features.Layout;
using XFramework.Client.Shared.Core.Features.Modals;
using XFramework.Client.Shared.Core.Features.Session;

namespace XFramework.Client.Shared.Components;

public class XComponentsBase : BlazorStateComponent
{
    // Inject Services
    [Inject] public HttpClient BaseHttpClient { get; set; }
    [Inject] public IHttpClient HttpClient { get; set; }
    [Inject] public NavigationManager NavigationManager { get; set; }
    [Inject] public ISessionStorageService SessionStorageService { get; set; }
    [Inject] public SweetAlertService SweetAlertService { get; set; }
    [Inject] public IJSRuntime JsRuntime { get; set; }
    [Inject] public EndPointsModel EndPoints { get; set; }
    [Inject] public IDialogService DialogService { get; set; }
    
    // Initialize States
    public ApplicationState AppState => GetState<ApplicationState>();
    public LayoutState LayoutState => GetState<LayoutState>();
    public SessionState SessionState => GetState<SessionState>();
    public ModalState ModalState => GetState<ModalState>();
    public CacheState CacheState => GetState<CacheState>();
    public CryptocurrencyState CryptocurrencyState => GetState<CryptocurrencyState>();

    public string Cursor => AppState.IsBusy ? "progress" : "arrow";
    
    // Global Methods
    public async Task NavigateTo(string path)
    {
        await Mediator.Send(new SessionState.NavigateToPath() {NavigationPath = path});
    }
    public async Task NavigateBack()
    {
        await Mediator.Send(new SessionState.NavigateBack());
    }
}