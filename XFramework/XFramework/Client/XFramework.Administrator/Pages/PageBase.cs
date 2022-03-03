using Blazored.SessionStorage;
using BlazorState;
using CurrieTechnologies.Razor.SweetAlert2;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using XFramework.Client.Shared.Core.Features.Session;
using XFramework.Client.Shared.Core.Helpers;
using XFramework.Client.Shared.Entity.Models.Components;

namespace XFramework.Administrator.Pages;

public class PageBase : BlazorStateComponent
{
   [Inject] public IHttpClient HttpClient { get; set; }
   [Inject] public NavigationManager NavigationManager { get; set; }
   [Inject] public ISessionStorageService SessionStorageService { get; set; }
   [Inject] public SweetAlertService SweetAlertService { get; set; }
   [Inject] public IJSRuntime JsRuntime { get; set; }
   [Inject] public EndPointsModel EndPoints { get; set; }

   public SessionState SessionState => GetState<SessionState>();
}