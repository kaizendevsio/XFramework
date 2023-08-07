namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Patient
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public Guid CredentialId { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    
    public virtual PatientType Type { get; set; } = null!;

    public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; } = new List<LaboratoryJobOrder>();

    public virtual ICollection<PatientAilment> PatientAilments { get; } = new List<PatientAilment>();

    public virtual ICollection<PatientConsultation> PatientConsultations { get; } = new List<PatientConsultation>();

    public virtual ICollection<PatientLaboratory> PatientLaboratories { get; } = new List<PatientLaboratory>();

    public virtual ICollection<PatientReminder> PatientReminders { get; } = new List<PatientReminder>();

    public virtual ICollection<PatientTag> PatientTags { get; } = new List<PatientTag>();

    public virtual ICollection<PharmacyJobOrder> PharmacyJobOrders { get; } = new List<PharmacyJobOrder>();
}
