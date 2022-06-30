using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Entity.Enums;

namespace XFramework.Client.Shared.Core.Features.Address;

public partial class AddressState
{
    public class FetchHandler : ActionHandler<Fetch, CmdResponse>
    {
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        private AddressState CurrentState => Store.GetState<AddressState>();

        public FetchHandler(IIdentityServiceWrapper identityServiceWrapper ,IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<CmdResponse> Handle(Fetch action, CancellationToken aCancellationToken)
        {
            switch (action.AddressType)
            {
                case AddressType.NotSpecified:
                    break;
                case AddressType.Country:
                    var countryResponse = await IdentityServiceWrapper.GetCountryList(new());
                    await HandleFailure(countryResponse, action, false);
                    await Mediator.Send(new SetState() {CountryList = countryResponse.Response});
                    break;
                case AddressType.Region:
                    var regionResponse = await IdentityServiceWrapper.GetRegionList(new(){Guid = Guid.Parse(CurrentState.SelectedCountry.Guid)});
                    await HandleFailure(regionResponse, action, false);
                    await Mediator.Send(new SetState() {RegionList = regionResponse.Response, SelectedRegion = new(), SelectedCity = new(), SelectedBarangay = new()});
                    break;
                case AddressType.Province:
                    var provinceResponse = await IdentityServiceWrapper.GetProvinceList(new(){Guid = Guid.Parse(CurrentState.SelectedRegion.Guid)});
                    await HandleFailure(provinceResponse, action, false);
                    await Mediator.Send(new SetState() {ProvinceList = provinceResponse.Response, SelectedProvince = new(), SelectedCity = new(), SelectedBarangay = new()});
                    break;
                case AddressType.City:
                    var cityResponse = await IdentityServiceWrapper.GetCityList(new(){Guid = Guid.Parse(CurrentState.SelectedProvince.Guid)});
                    await HandleFailure(cityResponse, action, false);
                    await Mediator.Send(new SetState() {CityList = cityResponse.Response, SelectedCity = new(), SelectedBarangay = new()});
                    break;
                case AddressType.Barangay:
                    var barangayResponse = await IdentityServiceWrapper.GetBarangayList(new(){Guid = Guid.Parse(CurrentState.SelectedCity.Guid)});
                    await HandleFailure(barangayResponse, action, false);
                    await Mediator.Send(new SetState() {BarangayList = barangayResponse.Response, SelectedBarangay = new()});
                    break;
            }

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                IsSuccess = true
            };
        }
    }
}