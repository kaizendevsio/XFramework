﻿using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetGroupMemberListHandler : ActionHandler<CommunityState.GetGroupMemberList>
    {
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        
        public GetGroupMemberListHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<Unit> Handle(GetGroupMemberList action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.GetConnectionList(new()
            {
                RequestServer = null,
                Limit = action.Limit <= 0
                    ? 1000
                    : action.Limit,
                CommunityIdentityGuid = Guid.Parse(CurrentState.CurrentCommunityGroup.Guid),
                ConnectionEntityGuid = Guid.Parse("49100f56-16c6-4ed9-b53a-7d00780f3451")
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