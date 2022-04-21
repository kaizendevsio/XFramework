using Blazored.LocalStorage;
using Community.Integration.Interfaces;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Community;

namespace XFramework.Client.Shared.Core.Features.User;

public partial class UserState
{
    public class CreateUserHandler : ActionHandler<CreateUser>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }

        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public CreateUserHandler(ICommunityServiceWrapper communityServiceWrapper , IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<Unit> Handle(CreateUser action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.CreateIdentity(new()
            {
                CredentialGuid = action.CredentialGuid,
                CommunityEntityGuid = Guid.Parse("ad043baa-be27-48a2-90ef-0f3f82cdb1c0")
            });
            
            return Unit.Value;
        }
    }
}