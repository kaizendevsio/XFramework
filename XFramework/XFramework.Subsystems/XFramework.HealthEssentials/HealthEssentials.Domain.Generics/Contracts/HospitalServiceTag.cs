namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalServiceTag
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid HospitalServiceId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }

    
    public virtual HospitalService HospitalService { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}
