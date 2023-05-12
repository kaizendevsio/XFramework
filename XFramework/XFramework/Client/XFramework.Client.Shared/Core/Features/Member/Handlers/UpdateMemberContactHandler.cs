namespace XFramework.Client.Shared.Core.Features.Member;

public partial class MemberState
{
    protected class UpdateMemberContactHandler : ActionHandler<UpdateMemberContact, CmdResponse>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public MemberState CurrentState => Store.GetState<MemberState>();

        public UpdateMemberContactHandler(IndexedDbService indexedDbService, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store, IWalletServiceWrapper walletServiceWrapper, IIdentityServiceWrapper identityServiceWrapper) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<CmdResponse> Handle(UpdateMemberContact action, CancellationToken aCancellationToken)
        {
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = true});

            foreach (var identityContact in CurrentState.SelectedMember.IdentityContacts)
            {
                var response = await IdentityServiceWrapper.UpdateContact(new()
                {
                    Value = identityContact.Value,
                    Guid = identityContact.Guid
                });
                
                if (await HandleFailure(response, action, true)) return response.Adapt<CmdResponse>();
            }
            
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = false});

            await HandleSuccess(new(){HttpStatusCode = HttpStatusCode.Accepted, Message = "Accepted"}, action);
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