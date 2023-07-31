using XFramework.Client.Shared.Entity.Models.Responses.CoinCap;

namespace XFramework.Client.Shared.Core.Features.Cryptocurrency;

public partial class CryptocurrencyState
{
    protected class GetAssetListHandler : ActionHandler<GetAssetList>
    {
        public CryptocurrencyState CurrentState => Store.GetState<CryptocurrencyState>();

        public GetAssetListHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            Configuration = configuration;
            SessionStorageService = sessionStorageService;
            LocalStorageService = localStorageService;
            SweetAlertService = sweetAlertService;
            NavigationManager = navigationManager;
            EndPoints = endPoints;
            HttpClient = httpClient;
            BaseHttpClient = baseHttpClient;
            JsRuntime = jsRuntime;
        }

        public override async Task Handle(GetAssetList action, CancellationToken aCancellationToken)
        {
            var result = await HttpClient.GetJsonAsync<AssetListResponse>("https://api.coincap.io/v2/assets?limit=50", false);
            if (!result.IsSuccess) return;

            await Mediator.Send(new SetState() {AssetList = result.Response.Data});
            return;
        }
    }
}