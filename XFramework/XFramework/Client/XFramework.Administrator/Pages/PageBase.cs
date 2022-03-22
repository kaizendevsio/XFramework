using XFramework.Administrator.Pages.Dashboard;
using XFramework.Client.Shared.Entity.Models;

namespace XFramework.Administrator.Pages;

public class PageBase : BlazorStateComponent
{
   [Inject] public IHttpClient HttpClient { get; set; }
   [Inject] public NavigationManager NavigationManager { get; set; }
   [Inject] public ISessionStorageService SessionStorageService { get; set; }
   [Inject] public SweetAlertService SweetAlertService { get; set; }
   [Inject] public IJSRuntime JsRuntime { get; set; }
   [Inject] public EndPointsModel EndPoints { get; set; }
   [Inject] public HttpClient httpClient { get; set; }

   public SessionState SessionState => GetState<SessionState>();
   public List<SampleModels> Model { get; set; } = new();
   public async Task NavigateTo(string url)
   {
      NavigationManager.NavigateTo(url);
   }

  
}