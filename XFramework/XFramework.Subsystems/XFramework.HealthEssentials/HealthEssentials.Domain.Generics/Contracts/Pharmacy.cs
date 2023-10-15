namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Pharmacy : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Slogan { get; set; }

    public string? Description { get; set; }

    public string? ChemicalComponent { get; set; }


    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? Logo { get; set; }

    public int Status { get; set; }

    public virtual PharmacyType Type { get; set; } = null!;

    public virtual ICollection<PharmacyLocation> PharmacyLocations { get; } = new List<PharmacyLocation>();

    public virtual ICollection<PharmacyMember> PharmacyMembers { get; } = new List<PharmacyMember>();

    public virtual ICollection<PharmacyService> PharmacyServices { get; } = new List<PharmacyService>();

    public virtual ICollection<PharmacyStock> PharmacyStocks { get; } = new List<PharmacyStock>();

    public virtual ICollection<PharmacyTag> PharmacyTags { get; } = new List<PharmacyTag>();
}