using System.Text.Json.Serialization;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyLocationResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    [JsonIgnore] public long PharmacyId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UnitNumber { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    [JsonIgnore] public long? BarangayId { get; set; }
    [JsonIgnore] public long? CityId { get; set; }
    public string? Subdivision { get; set; }
    [JsonIgnore] public long? RegionId { get; set; }
    public bool? MainAddress { get; set; }
    [JsonIgnore] public long? ProvinceId { get; set; }
    [JsonIgnore] public long? CountryId { get; set; }
    public string Guid { get; set; } = null!;
    public int? Status { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? AlternativePhone { get; set; }

    public PharmacyResponse? Pharmacy { get; set; }
    public List<PharmacyJobOrder> PharmacyJobOrders { get; set; }
    public List<PharmacyMember> PharmacyMembers { get; set; }
    public List<PharmacyService> PharmacyServices { get; set; }
    public AddressBarangayResponse? Barangay { get; set; }
    public AddressCityResponse? City { get; set; }
    public AddressRegionResponse? Region { get; set; }
    public AddressProvinceResponse? Province { get; set; }
    public AddressCountryResponse? Country { get; set; }

}