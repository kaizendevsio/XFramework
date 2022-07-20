using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetGroupListHandler : ActionHandler<GetGroupList>
    {
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        
        public GetGroupListHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
            ILocalStorageService localStorageService, SweetAlertService sweetAlertService,
            NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient,
            HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration,
            sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient,
            baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(GetGroupList action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.GetIdentityList(new()
            {
                Limit = action.Limit <= 0
                    ? 1000
                    : action.Limit,
                CommunityIdentityEntityGuid = Guid.Parse("9f9e535f-357a-46c4-934a-2cf4382e2397")
            });
            
            await HandleFailure(result, action);
            
            if(result.HttpStatusCode is not HttpStatusCode.Accepted) return Unit.Value;
            await Mediator.Send(new SetState(){CommunityGroupList = result.Response});
            return Unit.Value;
        }
    }
}