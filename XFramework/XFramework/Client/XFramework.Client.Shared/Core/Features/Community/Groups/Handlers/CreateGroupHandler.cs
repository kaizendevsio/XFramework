using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class CreateGroupHandler : ActionHandler<CreateGroup>
    {
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }

        public CreateGroupHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task Handle(CreateGroup action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.CreateIdentity(new()
            {
                Alias = action.Name,
                HandleName = action.HandleName,
                Tagline = action.Tagline,
                CredentialGuid = SessionState.Credential.Guid,
                CommunityEntityGuid = Guid.Parse("9f9e535f-357a-46c4-934a-2cf4382e2397")
            });
            
            await HandleFailure(result, action);
            await HandleSuccess(result, action);
            return;
        }
    }
}