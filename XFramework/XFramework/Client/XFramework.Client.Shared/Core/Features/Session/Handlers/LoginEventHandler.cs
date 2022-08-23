using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;

namespace XFramework.Client.Shared.Core.Features.Session;


public partial class SessionState
{
    protected class LoginEventHandler : EventHandler<LoginEvent>
    {
        public IMessageBusWrapper MessageBusWrapper { get; }


        public SessionState CurrentState => Store.GetState<SessionState>();

        public LoginEventHandler(IMessageBusWrapper messageBusWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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
        

        public override async Task Handle(LoginEvent action, CancellationToken cancellationToken)
        {
            if (action.StatusCode is HttpStatusCode.Accepted)
            {
                await MessageBusWrapper.StartClientEventListener(CurrentState.Credential.Guid);
            }
        }
    }
}
