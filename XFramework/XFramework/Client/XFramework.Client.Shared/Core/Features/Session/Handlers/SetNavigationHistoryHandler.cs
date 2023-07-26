using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BlazorState;
using CurrieTechnologies.Razor.SweetAlert2;
using MediatR;
using Microsoft.AspNetCore.Components;

namespace XFramework.Client.Shared.Core.Features.Session;


public partial class SessionState
{

    protected class SetNavigationHistoryHandler : ActionHandler<NavigateToPath>
    {
        
        public SessionState SessionState => Store.GetState<SessionState>();
        
        public SetNavigationHistoryHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task Handle(NavigateToPath action, CancellationToken cancellationToken)
        {
            SessionState.NavigationHistoryList.Add(NavigationManager.Uri);
            NavigationManager.NavigateTo(action.NavigationPath);
            
            if (action.NavigationPath == "/")
            {
                SessionState.NavigationHistoryList.Clear();
            }
            await Mediator.Send(new SetState() {NavigationHistoryList = SessionState.NavigationHistoryList});
            return;
        }

        
    }
}
