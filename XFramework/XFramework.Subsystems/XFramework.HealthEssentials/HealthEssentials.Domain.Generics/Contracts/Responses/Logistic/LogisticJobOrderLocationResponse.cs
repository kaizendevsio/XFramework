

using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticJobOrderLocationResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public short Status { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UnitNumber { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    public Guid? BarangayGuid { get; set; }
    public Guid? CityGuid { get; set; }
    public string? Subdivision { get; set; }
    public Guid? RegionGuid { get; set; }
    public bool? MainAddress { get; set; }
    public Guid? ProvinceGuid { get; set; }
    public Guid? CountryGuid { get; set; }
    public Guid? Guid { get; set; }
    public short Priority { get; set; }
    public bool IsDestination { get; set; }
    public string? ClientGuid { get; set; }
    public string? ClientName { get; set; }

    public LogisticJobOrderResponse? LogisticJobOrder { get; set; }
    public AddressBarangayResponse? Barangay { get; set; }
    public AddressCityResponse? City { get; set; }
    public AddressRegionResponse? Region { get; set; }
    public AddressProvinceResponse? Province { get; set; }
    public AddressCountryResponse? Country { get; set; }
}