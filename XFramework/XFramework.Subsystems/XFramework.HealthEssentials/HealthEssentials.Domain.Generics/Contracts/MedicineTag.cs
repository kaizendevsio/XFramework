namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineTag
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid MedicineId { get; set; }

    public string? Value { get; set; }

    
    public Guid TagId { get; set; }

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;
}
