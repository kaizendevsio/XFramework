using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetUserListHandler : ActionHandler<GetUserList, CmdResponse>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        private CommunityState CurrentState => Store.GetState<CommunityState>();

        public GetUserListHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<CmdResponse> Handle(GetUserList action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.GetIdentityList(new()
            {
                Limit = 10,
                CommunityIdentityEntityGuid = Guid.Parse("ad043baa-be27-48a2-90ef-0f3f82cdb1c0")
            });

            if (await HandleFailure(result, action))
            {
                return new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    
                };
            };

            await Mediator.Send(new SetState() {FriendSuggestionList = result.Response});
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                
            };
        }
    }
}