using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class CreateLaboratoryLocationRequest : RequestBase
{
    public long LaboratoryId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UnitNumber { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    public long? BarangayGuid { get; set; }
    public long? CityGuid { get; set; }
    public string? Subdivision { get; set; }
    public long? RegionGuid { get; set; }
    public bool? MainAddress { get; set; }
    public long? ProvinceGuid { get; set; }
    public long? CountryGuid { get; set; }
    public Guid? Guid { get; set; }
    public int? Status { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? AlternativePhone { get; set; }
}