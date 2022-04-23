using Blazored.LocalStorage;
using Community.Integration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class DeleteGroupMemberRequestHandler : ActionHandler<DeleteGroupMemberRequest>
    {
        private CommunityState CurrentState => Store.GetState<CommunityState>();
        public ICommunityServiceWrapper CommunityServiceWrapper { get; }

        public DeleteGroupMemberRequestHandler(ICommunityServiceWrapper communityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
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

        public override async Task<Unit> Handle(DeleteGroupMemberRequest action, CancellationToken aCancellationToken)
        {
            var result = await CommunityServiceWrapper.DeleteConnection(new()
            {
                Guid = action.Guid
            });
            
            await HandleFailure(result, action);
            await HandleSuccess(result, action);
            return Unit.Value;
        }
    }
}