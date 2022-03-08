using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class GetWalletListHandler : ActionHandler<GetWalletList>
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }

        public GetWalletListHandler(IWalletServiceWrapper walletServiceWrapper, IConfiguration configuration,
            ISessionStorageService sessionStorageService, ILocalStorageService localStorageService,
            SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints,
            IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) :
            base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager,
                endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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
 
        public override async Task<Unit> Handle(GetWalletList action, CancellationToken aCancellationToken)
        {
            if(SessionState.State is not Domain.Generic.Enums.SessionState.Active) return Unit.Value;
            var response = await WalletServiceWrapper.GetWalletList(new()
            {
                CredentialGuid = SessionState.Credential.Guid
            });

            // Handle if the response is invalid or error
            if (await HandleFailure(response, action, true)) return Unit.Value;

            // Set Session State To Active
            await Mediator.Send(new SetState() {WalletList = response.Response});

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, true);

            await Mediator.Send(new SetState
            {
                WalletList = response.Response
            });

            return Unit.Value;
        }
    }
}