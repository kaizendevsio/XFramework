using XFramework.Client.Shared.Core.Features.Layout;

namespace XFramework.Client.Shared.Components;

public class XLayoutBase : BlazorStateLayoutComponent
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
    [Inject] public IMediator Mediator { get; set; }
    
    
    public LayoutState LayoutState => GetState<LayoutState>();
    
    public async Task NavigateTo(string path)
    {
        await Mediator.Send(new SessionState.NavigateToPath() {NavigationPath = path});
    }
    public async Task NavigateBack()
    {
        await Mediator.Send(new SessionState.NavigateBack());
    }
}