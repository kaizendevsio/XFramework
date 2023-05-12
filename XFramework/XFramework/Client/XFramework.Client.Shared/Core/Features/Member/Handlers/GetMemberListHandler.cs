namespace XFramework.Client.Shared.Core.Features.Member;

public partial class MemberState
{
    protected class GetMemberListHandler : ActionHandler<GetMemberList, CmdResponse>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public MemberState CurrentState => Store.GetState<MemberState>();

        public GetMemberListHandler(IndexedDbService indexedDbService, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store, IWalletServiceWrapper walletServiceWrapper, IIdentityServiceWrapper identityServiceWrapper) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<CmdResponse> Handle(GetMemberList action, CancellationToken aCancellationToken)
        {
            ReportTask(action, CurrentState.MemberList);
            
            var response = await IdentityServiceWrapper.GetCredentialList(new()
            {
                ApplicationGuid = Guid.Parse(Configuration.GetValue<string>("Application:DefaultUID")),
                PageSize = 1000
            });
            
            ReportTaskCompleted(action);

            if (await HandleFailure(response, action, true)) return response.Adapt<CmdResponse>();
            await Mediator.Send(new SetState(){MemberList = response.Response});

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true,
                Message = "Success"
            };
        }
    }
}