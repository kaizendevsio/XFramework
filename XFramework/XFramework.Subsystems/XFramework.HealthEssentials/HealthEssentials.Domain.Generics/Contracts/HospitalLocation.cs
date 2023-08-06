namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalLocation
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid HospitalId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? UnitNumber { get; set; }

    public string? Street { get; set; }

    public string? Building { get; set; }

    public string? BarangayGuid { get; set; }

    public string? CityGuid { get; set; }

    public string? Subdivision { get; set; }

    public string? RegionGuid { get; set; }

    public bool? MainAddress { get; set; }

    public string? ProvinceGuid { get; set; }

    public string? CountryGuid { get; set; }

    
    public virtual Hospital Hospital { get; set; } = null!;

    public virtual ICollection<HospitalService> HospitalServices { get; } = new List<HospitalService>();
}
