using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class BlockFriendHandler: ActionHandler<BlockFriend>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        private CommunityState CurrentState => Store.GetState<CommunityState>();

        public BlockFriendHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<Unit> Handle(BlockFriend action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.CreateConnection(new()
            {
                SourceCommunityIdentityGuid = Guid.Parse(CurrentState.Identity.Guid),
                TargetCommunityIdentityGuid = action.CommunityIdentityGuid,
                CommunityConnectionEntityGuid = Guid.Parse("84a9e833-081b-46d6-8e42-b2dbc5173d7b")
            });
            
            await HandleFailure(result, action);
            await HandleSuccess(result, action);
            return Unit.Value;
        }
    }
}