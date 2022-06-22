using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyLocationResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long PharmacyId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UnitNumber { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    public long? Barangay { get; set; }
    public long? City { get; set; }
    public string? Subdivision { get; set; }
    public long? Region { get; set; }
    public bool? MainAddress { get; set; }
    public long? Province { get; set; }
    public long? Country { get; set; }
    public string Guid { get; set; } = null!;
    public int? Status { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? AlternativePhone { get; set; }
  
    public AddressBarangayResponse? BarangayNavigation { get; set; }
    public AddressCityResponse? CityNavigation { get; set; }
    public AddressCountryResponse? CountryNavigation { get; set; }
    public AddressProvinceResponse? ProvinceNavigation { get; set; }
    public AddressRegionResponse? RegionNavigation { get; set; }
}