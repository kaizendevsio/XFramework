using IdentityServer.Domain.Generic.Contracts.Requests.Update;

namespace XFramework.Client.Shared.Core.Features.Member;

public partial class MemberState
{
    protected class UpdateMemberInfoHandler : ActionHandler<UpdateMemberInfo, CmdResponse>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public MemberState CurrentState => Store.GetState<MemberState>();

        public UpdateMemberInfoHandler(IndexedDbService indexedDbService, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store, IWalletServiceWrapper walletServiceWrapper, IIdentityServiceWrapper identityServiceWrapper) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<CmdResponse> Handle(UpdateMemberInfo action, CancellationToken aCancellationToken)
        {
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = true});

            var response = await IdentityServiceWrapper.UpdateIdentity(CurrentState.SelectedMember.IdentityInfo.Adapt<UpdateIdentityRequest>());
            if (await HandleFailure(response, action, true)) return response.Adapt<CmdResponse>();

            await Mediator.Send(new ApplicationState.SetState(){IsBusy = false});
            await HandleSuccess(response, action);
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