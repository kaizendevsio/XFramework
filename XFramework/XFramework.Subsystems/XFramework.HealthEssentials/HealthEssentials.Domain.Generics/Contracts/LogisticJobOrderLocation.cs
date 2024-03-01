using System.ComponentModel.DataAnnotations.Schema;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LogisticJobOrderLocation : BaseModel
{
    public Guid LogisticJobOrderId { get; set; }

    public short Status { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? UnitNumber { get; set; }

    public string? Street { get; set; }

    public string? Building { get; set; }

    public Guid BarangayId { get; set; }

    public Guid CityId { get; set; }

    public string? Subdivision { get; set; }

    public Guid RegionId { get; set; }

    public bool? MainAddress { get; set; }

    public Guid ProvinceId { get; set; }

    public Guid CountryId { get; set; }


    public short Priority { get; set; }

    public bool IsDestination { get; set; }

    public string? ClientGuid { get; set; }

    public string? ClientName { get; set; }

    public virtual LogisticJobOrder LogisticJobOrder { get; set; } = null!;

    [NotMapped]
    public AddressRegion? Region { get; set; }
    
    [NotMapped]
    public AddressProvince? Province { get; set; }
    
    [NotMapped]
    public AddressCity? City { get; set; }
    
    [NotMapped]
    public AddressBarangay? Barangay { get; set; }
}