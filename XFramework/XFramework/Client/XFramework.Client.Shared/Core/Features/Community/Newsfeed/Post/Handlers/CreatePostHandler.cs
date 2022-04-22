using Blazored.LocalStorage;
using Community.Integration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class CreatePostHandler : ActionHandler<CreatePost>
    {
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }

        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public CreatePostHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<Unit> Handle(CreatePost action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.CreateContent(new()
            {
                Title = CurrentState.CurrentCommunityContent.Title,
                Text = CurrentState.CurrentCommunityContent.Text,
                ContentEntityGuid = Guid.Parse("57ef6c58-07d0-4c6a-aa1c-dd5f6812eb61"),
                CommunityIdentityGuid = Guid.Parse(CurrentState.Identity.Guid),
                CommunityGroupGuid = CurrentState.CurrentCommunityGroup is null ? null : Guid.Parse(CurrentState.CurrentCommunityGroup.Guid)
            });

            await HandleFailure(result, action);
            if (result.HttpStatusCode is not HttpStatusCode.Accepted) return Unit.Value;

            CurrentState.CurrentCommunityContent = new();
            
            await Mediator.Send(new SetState(){CurrentCommunityContent = CurrentState.CurrentCommunityContent});
            await Mediator.Send(new GetPostList());
            return Unit.Value;
        }
    }
}