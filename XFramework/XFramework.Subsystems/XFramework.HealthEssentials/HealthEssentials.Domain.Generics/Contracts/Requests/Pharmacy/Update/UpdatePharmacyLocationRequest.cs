using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

public class UpdatePharmacyLocationRequest : RequestBase
{
    public Guid? PharmacyGuid { get; set; }
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
    public GenericStatusType Status { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? AlternativePhone { get; set; }
}