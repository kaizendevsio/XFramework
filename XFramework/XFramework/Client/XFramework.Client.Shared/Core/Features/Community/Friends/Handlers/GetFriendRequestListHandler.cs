using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetFriendRequestListHandler : ActionHandler<GetFriendRequestList>
    {
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        
        public GetFriendRequestListHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<Unit> Handle(GetFriendRequestList action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.GetConnectionList(new()
            {
                Limit = action.Limit <= 0
                    ? 1000
                    : action.Limit,
                CommunityIdentityGuid = Guid.Parse(CurrentState.CurrentCommunityIdentity.Guid),
                ConnectionEntityGuid = Guid.Parse("6426a7f8-4e4b-4189-b9bc-815c912fcc78")
            });
            
            await HandleFailure(result, action);
            
            if(result.HttpStatusCode is not HttpStatusCode.Accepted) return Unit.Value;
            CurrentState.CurrentCommunityIdentity.ConnectionList ??= new();
            CurrentState.CurrentCommunityIdentity.ConnectionList?.AddRange(result.Response);
            
            await Mediator.Send(new SetState(){CurrentCommunityIdentity = CurrentState.CurrentCommunityIdentity});
            return Unit.Value;
        }
    }
}