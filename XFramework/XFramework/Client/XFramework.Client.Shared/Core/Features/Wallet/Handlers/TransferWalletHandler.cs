namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    protected class TransferWalletHandler : ActionHandler<TransferWallet>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }

        public WalletState CurrentState => Store.GetState<WalletState>();
        public TransferWalletHandler(IWalletServiceWrapper walletServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            WalletServiceWrapper = walletServiceWrapper;
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

        public override async Task<Unit> Handle(TransferWallet action, CancellationToken aCancellationToken)
        {
            if (HandleValidation(out var handle)) return handle;
            await Mediator.Send(new GetWalletList());
            
            var result = await WalletServiceWrapper.TransferWallet(new()
            {
                ClientReference = CurrentState.SendWalletVm.ClientReference,
                CredentialGuid = SessionState.Credential.Guid,
                WalletEntityGuid = CurrentState.SendWalletVm.WalletEntityGuid,
                Recipient = CurrentState.SendWalletVm.Recipient,
                Amount = CurrentState.SendWalletVm.Amount,
                Remarks = CurrentState.SendWalletVm.Remarks,

            });

            if (result.HttpStatusCode is HttpStatusCode.Accepted)
            {
                Mediator.Send(new GetWalletList());
                CurrentState.SendWalletVm.Action?.Invoke();
            }
            
            await HandleFailure(result, action);
            await HandleSuccess(result, action, false, "Payment Successful");
            
            return Unit.Value;
        }
        
        private bool HandleValidation(out Unit handle)
        {
            var wallet = CurrentState.WalletList.FirstOrDefault(i => i.WalletEntity.Guid == CurrentState.SendWalletVm.WalletEntityGuid);
            if (wallet is null)
            {
                SweetAlertService.FireAsync("Error", "You don't have the selected wallet");
                {
                    handle = Unit.Value;
                    return true;
                }
            }

            if (wallet.Balance <= 0)
            {
                SweetAlertService.FireAsync("Error", "You don't have any balance");
                {
                    handle = Unit.Value;
                    return true;
                }
            }

            if (CurrentState.SendWalletVm.Recipient == $"{SessionState.Credential.UserName}" ||
                SessionState.ContactList.Any(i => i.Value == CurrentState.SendWalletVm.Recipient))
            {
                SweetAlertService.FireAsync("Error", "You can't send to your own account");
                {
                    handle = Unit.Value;
                    return true;
                }
            }

            return false;
        }
    }
}