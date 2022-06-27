using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy;

public class CreatePharmacyLocationRequest : RequestBase
{
    public long PharmacyId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UnitNumber { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    public long? BarangayId { get; set; }
    public long? CityId { get; set; }
    public string? Subdivision { get; set; }
    public long? RegionId { get; set; }
    public bool? MainAddress { get; set; }
    public long? ProvinceId { get; set; }
    public long? CountryId { get; set; }
    public Guid? Guid { get; set; }
    public int? Status { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? AlternativePhone { get; set; }
}