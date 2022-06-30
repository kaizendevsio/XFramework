using Blazored.LocalStorage;
using Community.Integration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class UpdateReactionHandler : ActionHandler<UpdateReaction>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        
        public UpdateReactionHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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
        public override async Task<Unit> Handle(UpdateReaction action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.UpdateReaction(new()
            {
                Guid = action.Guid,
                ReactionEntityGuid = action.ReactionEntityGuid
            });
            
            await HandleFailure(result, action);
            if (result.HttpStatusCode is not HttpStatusCode.Accepted) return Unit.Value;
            
            var updatedContent = await CommunityServiceWrapper.GetContent(new()
            {
                Guid = action.ContentGuid
            });

            if(updatedContent.HttpStatusCode is not HttpStatusCode.Accepted) return Unit.Value;
            var contentResponse = CurrentState.NewsFeedContentList.FirstOrDefault(i => i.Guid == action.ContentGuid);
            contentResponse = updatedContent.Response;

            await Mediator.Send(new SetState() {NewsFeedContentList = CurrentState.NewsFeedContentList});
            return Unit.Value;
        }
    }
}