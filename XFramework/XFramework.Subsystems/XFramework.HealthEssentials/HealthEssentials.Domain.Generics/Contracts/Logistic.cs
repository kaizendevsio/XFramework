namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Logistic
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    
    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? Logo { get; set; }

    public int Status { get; set; }

    public virtual LogisticType Type { get; set; } = null!;

    public virtual ICollection<LogisticRiderHandle> LogisticRiderHandles { get; } = new List<LogisticRiderHandle>();
}
