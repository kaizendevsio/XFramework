namespace IdentityServer.Domain.Generic.Contracts.Responses.Address;

public class IdentityAddressResponse
{
    public string UnitNumber { get; set; }
    public string Street { get; set; }
    public string Building { get; set; }
    public long? Barangay { get; set; }
    public long? City { get; set; }
    public string Subdivision { get; set; }
    public long? Region { get; set; }
    public long? AddressEntitiesId { get; set; }
    public bool? DefaultAddress { get; set; }
    public long? Province { get; set; }
    public long? Country { get; set; }
    public string Guid { get; set; }
    
    public AddressBarangayResponse BarangayNavigation { get; set; }
    public AddressCityResponse CityNavigation { get; set; }
    public AddressCountryResponse CountryNavigation { get; set; }
    public AddressProvinceResponse ProvinceNavigation { get; set; }
    public AddressRegionResponse RegionNavigation { get; set; }
}