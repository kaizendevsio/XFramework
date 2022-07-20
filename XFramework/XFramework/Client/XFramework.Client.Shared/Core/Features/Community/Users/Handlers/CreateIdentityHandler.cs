using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class CreateIdentityHandler : ActionHandler<CreateIdentity, CmdResponse>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        private CommunityState CurrentState => Store.GetState<CommunityState>();

        public CreateIdentityHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<CmdResponse> Handle(CreateIdentity action, CancellationToken aCancellationToken)
        {
            Console.WriteLine("Creating Identity Handler");
            var result = await CommunityServiceWrapper.CreateIdentity(new()
            {
                Alias = action.Alias,
                HandleName = action.HandleName,
                Tagline = action.Tagline,
                CredentialGuid = action.CredentialGuid,
                CommunityEntityGuid = Guid.Parse("ad043baa-be27-48a2-90ef-0f3f82cdb1c0")
            });

            if (await HandleFailure(result, action))
            {
                return new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    IsSuccess = false
                };
            };
            
            await HandleSuccess(result, action, true);
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true
            };
        }
    }
}