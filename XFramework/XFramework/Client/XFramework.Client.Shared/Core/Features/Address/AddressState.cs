using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace XFramework.Client.Shared.Core.Features.Address;

public partial class AddressState : State<AddressState>
{
    public override void Initialize()
    {
        
    }

    public List<AddressCountryResponse> CountryList { get; set; }
    public List<AddressRegionResponse> RegionList { get; set; }
    public List<AddressProvinceResponse> ProvinceList { get; set; }
    public List<AddressCityResponse> CityList { get; set; }
    public List<AddressBarangayResponse> BarangayList { get; set; }
    
    public AddressCountryResponse SelectedCountry { get; set; } = new();
    public AddressRegionResponse SelectedRegion { get; set; } = new();
    public AddressProvinceResponse SelectedProvince { get; set; } = new();
    public AddressCityResponse SelectedCity { get; set; } = new();
    public AddressBarangayResponse SelectedBarangay { get; set; } = new();

}