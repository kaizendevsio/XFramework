using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Layout;

public partial class LayoutState
{
    protected class UpdateSidebarItemsHandler : ActionHandler<UpdateSidebarItems>
    {
        public UpdateSidebarItemsHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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
            
        public override async Task<Unit> Handle(UpdateSidebarItems a, CancellationToken aCancellationToken)
        {
            /*var newSidebarItems = SessionState.TenantAccountPersonaRole.Value.Select(item => new SidebarItemInfo() { Text = item.PersonaBubbleModule.BubbleModule.Name }).ToList();
            await Mediator.Send(new SetState() { SidebarInfo = new SidebarInfo() { Items = newSidebarItems } });*/

            return Unit.Value;
        }

        
    }
}