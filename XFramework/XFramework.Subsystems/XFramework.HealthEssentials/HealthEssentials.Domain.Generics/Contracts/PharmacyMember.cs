namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyMember
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string CredentialGuid { get; set; } = null!;

    public Guid PharmacyId { get; set; }

    public string? Value { get; set; }

    
    public string Name { get; set; } = null!;

    public int Status { get; set; }

    public string? Description { get; set; }

    public Guid PharmacyLocationId { get; set; }

    public virtual Pharmacy Pharmacy { get; set; } = null!;

    public virtual PharmacyLocation? PharmacyLocation { get; set; }
}
