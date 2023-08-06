namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyTag
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid PharmacyId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }

    
    public virtual Pharmacy Pharmacy { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}
