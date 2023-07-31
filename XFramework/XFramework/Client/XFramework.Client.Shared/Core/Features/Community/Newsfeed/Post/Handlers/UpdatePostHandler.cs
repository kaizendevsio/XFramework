using Community.Integration.Interfaces;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class UpdatePostHandler : ActionHandler<UpdatePost>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public UpdatePostHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task Handle(UpdatePost action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.UpdateContent(new()
            {
                Title = CurrentState.CurrentCommunityContent.Title,
                Text = CurrentState.CurrentCommunityContent.Text,
                SocialMediaIdentityGuid = System.Guid.Parse(CurrentState.Identity.Guid),
                Guid = CurrentState.CurrentCommunityContent.Guid
            });

            await HandleFailure(result, action);
            if (result.HttpStatusCode is not HttpStatusCode.Accepted) return;
            
            await Mediator.Send(new GetPostList());
            return;
        }
    }
}