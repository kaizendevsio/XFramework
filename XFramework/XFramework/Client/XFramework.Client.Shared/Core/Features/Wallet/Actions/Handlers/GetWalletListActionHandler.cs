using Blazored.LocalStorage;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Wallet;


public partial class WalletState
{
    public class GetWalletListActionHandler : ActionHandler<GetWalletListAction>

    {
        public GetWalletListActionHandler(ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
        }

        public override async Task<Unit> Handle(GetWalletListAction action, CancellationToken aCancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}