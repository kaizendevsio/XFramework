using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Events;

public partial class EventState
{
    public class UpdateEventHandler : ActionHandler<UpdateEvent>
    {
        public UpdateEventHandler(IConfiguration configuration, ISessionStorageService sessionStorageService,
            ILocalStorageService localStorageService, SweetAlertService sweetAlertService,
            NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient,
            HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration,
            sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient,
            baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(UpdateEvent action, CancellationToken aCancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}