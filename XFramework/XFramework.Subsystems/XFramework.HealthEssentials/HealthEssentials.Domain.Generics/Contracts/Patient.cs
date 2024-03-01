using System.ComponentModel.DataAnnotations.Schema;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Patient : BaseModel
{
    public Guid TypeId { get; set; }

    public Guid CredentialId { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }


    public virtual PatientType Type { get; set; } = null!;

    public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; set; } = new List<LaboratoryJobOrder>();

    public virtual ICollection<PatientAilment> PatientAilments { get; set; } = new List<PatientAilment>();

    public virtual ICollection<PatientConsultation> PatientConsultations { get; set; } = new List<PatientConsultation>();

    public virtual ICollection<PatientLaboratory> PatientLaboratories { get; set; } = new List<PatientLaboratory>();

    public virtual ICollection<PatientReminder> PatientReminders { get; set; } = new List<PatientReminder>();

    public virtual ICollection<PatientTag> PatientTags { get; set; } = new List<PatientTag>();

    public virtual ICollection<PharmacyJobOrder> PharmacyJobOrders { get; set; } = new List<PharmacyJobOrder>();

    [NotMapped]
    public IdentityCredential? Credential { get; set; }
}