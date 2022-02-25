using Blazored.LocalStorage;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Wallet;

public partial class WalletState
{
    public class TransferWalletActionHandler : ActionHandler<TransferWalletAction>
    {
        public TransferWalletActionHandler(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
        }

        public override async Task<Unit> Handle(TransferWalletAction action, CancellationToken aCancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}