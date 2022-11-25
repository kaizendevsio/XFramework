﻿using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetGroupPostListHandler : ActionHandler<GetGroupPostList>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public GetGroupPostListHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(GetGroupPostList action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.GetContentList(new()
            {
                Limit = 50,
                GreaterThan = CurrentState.LastPull,
                ContentEntityGuid = Guid.Parse("57ef6c58-07d0-4c6a-aa1c-dd5f6812eb61"),
                CommunityIdentityGuid = System.Guid.Parse(CurrentState.Identity.Guid),

            });

            await HandleFailure(result, action);
            if (result.HttpStatusCode is not HttpStatusCode.Accepted) return Unit.Value;
            
            CurrentState.NewsFeedContentList.AddRange(result.Response);
            await Mediator.Send(new SetState() {NewsFeedContentList = CurrentState.NewsFeedContentList, LastPull = DateTime.Now});
            return Unit.Value;
        }
    }
}