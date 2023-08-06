namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PatientAilment
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid PatientId { get; set; }

    public Guid AilmentId { get; set; }

    public string? Remarks { get; set; }

    
    public virtual Ailment Ailment { get; set; } = null!;

    public virtual Patient Patient { get; set; } = null!;

    public virtual ICollection<PatientAilmentDetail> PatientAilmentDetails { get; } = new List<PatientAilmentDetail>();
}
