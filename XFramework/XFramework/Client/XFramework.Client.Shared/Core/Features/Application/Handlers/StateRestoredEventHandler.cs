using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;

namespace XFramework.Client.Shared.Core.Features.Application;


public partial class ApplicationState
{
    protected class StateRestoredEventHandler : EventHandler<StateRestoredEvent>
    {
        public IMessageBusWrapper MessageBusWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();

        public StateRestoredEventHandler(IMessageBusWrapper messageBusWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            MessageBusWrapper = messageBusWrapper;
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
        

        public override async Task Handle(StateRestoredEvent action, CancellationToken cancellationToken)
        {
            await MessageBusWrapper.StartClientEventListener(CurrentState.Credential.Guid);
        }
    }
}
