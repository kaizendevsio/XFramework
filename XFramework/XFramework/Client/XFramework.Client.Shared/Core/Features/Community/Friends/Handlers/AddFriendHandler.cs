using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class AddFriendHandler: ActionHandler<AddFriend>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        private CommunityState CurrentState => Store.GetState<CommunityState>();

        public AddFriendHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task Handle(AddFriend action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.CreateConnection(new()
            {
                SourceCommunityIdentityGuid = Guid.Parse(CurrentState.Identity.Guid),
                TargetCommunityIdentityGuid = action.CommunityIdentityGuid,
                CommunityConnectionEntityGuid = Guid.Parse("b53ac35a-c815-4a11-ad74-364d1b6cf84b")
            });
            
            await HandleFailure(result, action);
            await HandleSuccess(result, action);
            return;
        }
    }
}