namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryMember
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CredentialId { get; set; }

    public Guid LaboratoryId { get; set; }

    public string? Value { get; set; }

    
    public string Name { get; set; } = null!;

    public int Status { get; set; }

    public string? Description { get; set; }

    public Guid LaboratoryLocationId { get; set; }

    public virtual Laboratory Laboratory { get; set; } = null!;

    public virtual LaboratoryLocation? LaboratoryLocation { get; set; }
}
