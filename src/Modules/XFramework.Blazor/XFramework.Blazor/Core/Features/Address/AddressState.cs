using XFramework.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Address;

public partial class AddressState : State<AddressState>
{
    public override void Initialize()
    {
        
    }

    public List<AddressCountry>? CountryList { get; set; }
    public List<AddressRegion>? RegionList { get; set; }
    public List<AddressProvince>? ProvinceList { get; set; }
    public List<AddressCity>? CityList { get; set; }
    public List<AddressBarangay>? BarangayList { get; set; }
    
    public AddressCountry SelectedCountry { get; set; } = new();
    public AddressRegion SelectedRegion { get; set; } = new();
    public AddressProvince SelectedProvince { get; set; } = new();
    public AddressCity SelectedCity { get; set; } = new();
    public AddressBarangay SelectedBarangay { get; set; } = new();
    public string? CurrentAddressName { get; set; }
    public string? CurrentUnitNumber { get; set; }

}