using System;
using System.Linq;
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
    protected class NavigateBackHandler : ActionHandler<NavigateBack>
    {
        
        public SessionState SessionState => Store.GetState<SessionState>();

        public NavigateBackHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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
        

        public override async Task<Unit> Handle(NavigateBack action, CancellationToken cancellationToken)
        {
            NavigationManager.NavigateTo(SessionState.NavigationHistoryList.Last());
            SessionState.NavigationHistoryList.Remove(SessionState.NavigationHistoryList.Last());
            await Mediator.Send(new SetState() {NavigationHistoryList = SessionState.NavigationHistoryList});
            return Unit.Value;
        }

        
    }
}
