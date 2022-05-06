using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace XFramework.Client.Shared.Core.Features.Address;

public partial class AddressState
{
    public class SetState : IAction
    {
        public List<AddressCountryResponse> CountryList { get; set; }
        public List<AddressRegionResponse> RegionList { get; set; }
        public List<AddressProvinceResponse> ProvinceList { get; set; }
        public List<AddressCityResponse> CityList { get; set; }
        public List<AddressBarangayResponse> BarangayList { get; set; }
    
        public AddressCountryResponse SelectedCountry { get; set; }
        public AddressRegionResponse SelectedRegion { get; set; }
        public AddressProvinceResponse SelectedProvince { get; set; }
        public AddressCityResponse SelectedCity { get; set; }
        public AddressBarangayResponse SelectedBarangay { get; set; }
    }
}