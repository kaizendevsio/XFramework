namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyLocation
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid PharmacyId { get; set; }

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

    
    public int? Status { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? AlternativePhone { get; set; }

    public virtual Pharmacy Pharmacy { get; set; } = null!;

    public virtual ICollection<PharmacyJobOrder> PharmacyJobOrders { get; } = new List<PharmacyJobOrder>();

    public virtual ICollection<PharmacyMember> PharmacyMembers { get; } = new List<PharmacyMember>();

    public virtual ICollection<PharmacyService> PharmacyServices { get; } = new List<PharmacyService>();
}
