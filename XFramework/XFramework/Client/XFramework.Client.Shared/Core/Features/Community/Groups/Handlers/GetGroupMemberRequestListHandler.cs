using Blazored.LocalStorage;
using Community.Integration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetGroupMemberRequestListHandler : ActionHandler<GetGroupMemberRequestList>
    {
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        
        public GetGroupMemberRequestListHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<Unit> Handle(GetGroupMemberRequestList action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.GetConnectionList(new()
            {
                RequestServer = null,
                Limit = action.Limit <= 0
                    ? 1000
                    : action.Limit,
                CommunityIdentityGuid = Guid.Parse(CurrentState.CurrentCommunityGroup.Guid),
                ConnectionEntityGuid = Guid.Parse("5cf3baa6-8d15-4b4b-97de-b99e1c219ce2")
            });
            
            await HandleFailure(result, action);
            
            if(result.HttpStatusCode is not HttpStatusCode.Accepted) return Unit.Value;
            CurrentState.CurrentCommunityGroup.ConnectionList ??= new();
            CurrentState.CurrentCommunityGroup.ConnectionList?.AddRange(result.Response);
            
            await Mediator.Send(new SetState(){CurrentCommunityGroup = CurrentState.CurrentCommunityGroup});
            return Unit.Value;
        }
    }
}