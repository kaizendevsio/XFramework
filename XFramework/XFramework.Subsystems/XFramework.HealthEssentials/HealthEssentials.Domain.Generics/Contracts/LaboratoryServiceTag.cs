namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryServiceTag
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid LaboratoryServiceId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }

    
    public virtual LaboratoryService LaboratoryService { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}
