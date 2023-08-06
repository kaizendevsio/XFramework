namespace HealthEssentials.Domain.Generics.Contracts;

public partial class AilmentTag
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid AilmentId { get; set; }

    public string? Value { get; set; }

    
    public Guid TagId { get; set; }

    public virtual Ailment Ailment { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;
}
