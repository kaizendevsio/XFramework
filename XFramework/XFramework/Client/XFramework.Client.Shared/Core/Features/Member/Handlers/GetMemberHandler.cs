using Mapster;

namespace XFramework.Client.Shared.Core.Features.Member;

public partial class MemberState
{
    protected class GetMemberHandler : ActionHandler<GetMember, CmdResponse>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public MemberState CurrentState => Store.GetState<MemberState>();

        public GetMemberHandler(IndexedDbService indexedDbService, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store, IWalletServiceWrapper walletServiceWrapper, IIdentityServiceWrapper identityServiceWrapper) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<CmdResponse> Handle(GetMember action, CancellationToken aCancellationToken)
        {
            //ReportTask(action);

            var response1 = IdentityServiceWrapper.GetCredential(new() { Guid = action.CredentialGuid});
            var response2 = WalletServiceWrapper.GetWalletList(new() { CredentialGuid = action.CredentialGuid});

            await Task.WhenAll(response1, response2);
            //ReportTask(action, true);

            if (await HandleFailure(response1.Result, action, true)) return response1.Result.Adapt<CmdResponse>();

            CurrentState.SelectedMember = response1.Result.Response;
            CurrentState.SelectedMember.Wallets = response2.Result.Response;
            await Mediator.Send(new SetState(){SelectedMember = CurrentState.SelectedMember});

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Success",
                IsSuccess = true,
            };
        }
    }
}