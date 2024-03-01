namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalLocation : BaseModel
{
    public Guid HospitalId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? UnitNumber { get; set; }

    public string? Street { get; set; }

    public string? Building { get; set; }

    public Guid? BarangayId { get; set; }

    public Guid CityId { get; set; }

    public string? Subdivision { get; set; }

    public Guid RegionId { get; set; }

    public bool? MainAddress { get; set; }

    public Guid ProvinceId { get; set; }

    public Guid CountryId { get; set; }


    public virtual Hospital Hospital { get; set; } = null!;

    public virtual ICollection<HospitalService> HospitalServices { get; set; } = new List<HospitalService>();
}