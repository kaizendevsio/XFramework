using Mapster;

namespace XFramework.Client.Shared.Core.Features.Member;

public partial class MemberState
{
    protected class GetUnregisteredSubscriberListHandler : ActionHandler<GetUnregisteredSubscriberList, CmdResponse>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public MemberState CurrentState => Store.GetState<MemberState>();

        public GetUnregisteredSubscriberListHandler(IndexedDbService indexedDbService, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store, IWalletServiceWrapper walletServiceWrapper, IIdentityServiceWrapper identityServiceWrapper) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            WalletServiceWrapper = walletServiceWrapper;
            IdentityServiceWrapper = identityServiceWrapper;
            SessionStorageService = sessionStorageService;
            IndexedDbService = indexedDbService;
            Configuration = configuration;
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

        public override async Task<CmdResponse> Handle(GetUnregisteredSubscriberList action, CancellationToken aCancellationToken)
        {
            ReportTask(action);
            
            var unregisteredSubscriberList = await IdentityServiceWrapper.GetUnregisteredSubscriberList(new());
            
            ReportTask(action, true);

            if (await HandleFailure(unregisteredSubscriberList, action, true)) return unregisteredSubscriberList.Adapt<CmdResponse>();
            await Mediator.Send(new SetState(){UnregisteredSubscriber = unregisteredSubscriberList.Response});
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true,
                Message = "Success"
            };
        }
    }
}