using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class UpdateCommentHandler : ActionHandler<UpdateComment>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        
        public UpdateCommentHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task Handle(UpdateComment action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.UpdateContent(new()
            {
                Text = action.Text,
                Guid = action.ContentGuid
            });

            await HandleFailure(result, action);
            if (result.HttpStatusCode is not HttpStatusCode.Accepted) return;
            
            var updatedContent = await CommunityServiceWrapper.GetContent(new()
            {
                Guid = action.ContentGuid
            });

            if(updatedContent.HttpStatusCode is not HttpStatusCode.Accepted) return;
            var contentResponse = CurrentState.NewsFeedContentList.FirstOrDefault(i => i.Guid == action.ContentGuid);
            contentResponse = updatedContent.Response;

            await Mediator.Send(new SetState() {NewsFeedContentList = CurrentState.NewsFeedContentList});
            return;
        }
    }
}