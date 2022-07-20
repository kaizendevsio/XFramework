using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class AddGroupMemberHandler : ActionHandler<AddGroupMember>
    {
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }

        public AddGroupMemberHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<Unit> Handle(AddGroupMember action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.CreateConnection(new()
            {
                SourceCommunityIdentityGuid = Guid.Parse(CurrentState.Identity.Guid),
                TargetCommunityIdentityGuid = Guid.Parse(CurrentState.CurrentCommunityGroup.Guid),
                CommunityConnectionEntityGuid = Guid.Parse("49100f56-16c6-4ed9-b53a-7d00780f3451")
            });
            
            await HandleFailure(result, action);
            await HandleSuccess(result, action);
            return Unit.Value;
        }
    }
}