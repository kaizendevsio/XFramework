using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetPostHandler : ActionHandler<GetPost>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }

        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public GetPostHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            CommunityServiceWrapper = communityServiceWrapper;
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

        public override async Task<Unit> Handle(GetPost action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.GetContent(new()
            {
                Guid = action.ContentGuid
            });

            await HandleFailure(result, action);
            if (result.HttpStatusCode is not HttpStatusCode.Accepted) return Unit.Value;
            
            await Mediator.Send(new SetState() {CurrentCommunityContent = result.Response});
            return Unit.Value;
        }
    }
}

