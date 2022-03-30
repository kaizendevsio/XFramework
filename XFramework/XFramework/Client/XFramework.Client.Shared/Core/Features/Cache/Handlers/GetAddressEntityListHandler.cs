using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Client.Shared.Core.Features.Cache;

public partial class CacheState
{
    protected class GetAddressEntityListHandler : ActionHandler<GetAddressEntityList>
    {
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public CacheState CurrentState => Store.GetState<CacheState>();
        
        public GetAddressEntityListHandler(IIdentityServiceWrapper identityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            IdentityServiceWrapper = identityServiceWrapper;
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

        public override async Task<Unit> Handle(GetAddressEntityList action, CancellationToken aCancellationToken)
        {
            if(CurrentState.AddressEntityList is not null && CurrentState.AddressEntityList.Any()) return Unit.Value;
            
            var result = await IdentityServiceWrapper.GetAddressEntityList(new());
            if (!await HandleFailure(result, action, false))
            {
                await Mediator.Send(new SetState() {AddressEntityList = result.Response});
            }
            return Unit.Value;
        }
    }
}