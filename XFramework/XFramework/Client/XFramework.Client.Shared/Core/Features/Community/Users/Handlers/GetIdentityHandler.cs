using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetIdentityHandler : ActionHandler<GetIdentity, CmdResponse>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        private CommunityState CurrentState => Store.GetState<CommunityState>();

        public GetIdentityHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<CmdResponse> Handle(GetIdentity action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.GetIdentity(new()
            {
                CredentialGuid = action.CredentialGuid,
                CommunityIdentityGuid = action.CommunityIdentityGuid
            });

            if (await HandleFailure(result, action, true))
            {
                return new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    IsSuccess = false
                };
            };

            if (action.CredentialGuid is not null)
            {
                await Mediator.Send(new SetState() {Identity = result.Response});
            }
            
            if (action.CommunityIdentityGuid is not null)
            {
                await Mediator.Send(new SetState() {CurrentCommunityIdentity = result.Response});
            }
            
            await HandleSuccess(result, action, true);
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true
            };
        }
    }
}