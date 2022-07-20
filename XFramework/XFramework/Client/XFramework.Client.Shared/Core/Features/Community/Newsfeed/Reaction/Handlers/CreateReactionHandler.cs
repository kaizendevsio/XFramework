using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class CreateReactionHandler : ActionHandler<CreateReaction>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        
        public CreateReactionHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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
        public override async Task<Unit> Handle(CreateReaction action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.CreateReaction(new()
            {
                ContentGuid = action.ContentGuid,
                ReactionEntityGuid = action.ReactionEntityGuid,
                CommunityIdentityGuid = Guid.Parse(CurrentState.Identity.Guid),
            });
            
            await HandleFailure(result, action);
            if (result.HttpStatusCode is not HttpStatusCode.Accepted) return Unit.Value;
            
            /*var updatedContent = await CommunityServiceWrapper.GetContent(new()
            {
                Guid = action.ContentGuid
            });

            if(updatedContent.HttpStatusCode is not HttpStatusCode.Accepted) return Unit.Value;
            var contentResponse = CurrentState.NewsFeedContentList.FirstOrDefault(i => i.Guid == action.ContentGuid);
            contentResponse = updatedContent.Response;

            await Mediator.Send(new SetState() {NewsFeedContentList = CurrentState.NewsFeedContentList});*/

            await Mediator.Send(new GetPostList());
            return Unit.Value;
        }
    }
}