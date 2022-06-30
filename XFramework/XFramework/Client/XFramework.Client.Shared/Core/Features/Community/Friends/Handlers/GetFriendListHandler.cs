using Blazored.LocalStorage;
using Community.Integration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetFriendListHandler : ActionHandler<GetFriendList>
    {
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        
        public GetFriendListHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<Unit> Handle(GetFriendList action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.GetConnectionList(new()
            {
                Limit = action.Limit <= 0
                    ? 1000
                    : action.Limit,
                CommunityIdentityGuid = Guid.Parse(CurrentState.CurrentCommunityIdentity.Guid),
                ConnectionEntityGuid = Guid.Parse("b53ac35a-c815-4a11-ad74-364d1b6cf84b")
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