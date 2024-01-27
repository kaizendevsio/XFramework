using Address.Integration.Drivers;
using XFramework.Client.Shared.Entity.Enums;

namespace XFramework.Client.Shared.Core.Features.Address;

public partial class AddressState
{
    public record Fetch : StateAction
    {
        public AddressType AddressType { get; set; }
    }

    protected class FetchHandler(
        IAddressServiceWrapper addressServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<Fetch>(handlerServices, store)
    {
        private AddressState CurrentState => Store.GetState<AddressState>();
        
        public override async Task Handle(Fetch action, CancellationToken aCancellationToken)
        {
            switch (action.AddressType)
            {
                case AddressType.NotSpecified:
                    break;
                case AddressType.Country:
                    var countryResponse = await addressServiceWrapper.AddressCountry.GetList(500, 1);
                    await HandleFailure(countryResponse, action, false);
                    CurrentState.CountryList = countryResponse.Response?.Items.ToList();
                    break;
                case AddressType.Region:
                    var regionResponse = await addressServiceWrapper.AddressRegion.GetList(500, 1);
                    await HandleFailure(regionResponse, action, false);
                    CurrentState.RegionList = regionResponse.Response?.Items.ToList();
                    CurrentState.SelectedRegion = new();
                    CurrentState.SelectedCity = new();
                    CurrentState.SelectedBarangay = new();
                    break;
                case AddressType.Province:
                    var provinceResponse = await addressServiceWrapper.AddressProvince.GetList(500, 1);
                    await HandleFailure(provinceResponse, action, false);
                    CurrentState.ProvinceList = provinceResponse.Response?.Items.ToList(); 
                    CurrentState.SelectedProvince = new(); 
                    CurrentState.SelectedCity = new();
                    CurrentState.SelectedBarangay = new();
                    break;
                case AddressType.City:
                    var cityResponse = await addressServiceWrapper.AddressCity.GetList(500, 1);
                    await HandleFailure(cityResponse, action, false);
                    CurrentState.CityList = cityResponse.Response?.Items.ToList();
                    CurrentState.SelectedCity = new();
                    CurrentState.SelectedBarangay = new();
                    break;
                case AddressType.Barangay:
                    var barangayResponse = await addressServiceWrapper.AddressBarangay.GetList(500, 1);
                    await HandleFailure(barangayResponse, action, false);
                    CurrentState.BarangayList = barangayResponse.Response?.Items.ToList();
                    CurrentState.SelectedBarangay = new();
                    break;
            }
            
            store.SetState(CurrentState);
            
        }
    }
}