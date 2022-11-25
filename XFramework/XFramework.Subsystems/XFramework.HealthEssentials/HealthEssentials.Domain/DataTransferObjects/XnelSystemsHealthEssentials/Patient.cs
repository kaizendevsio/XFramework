using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class Patient
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long EntityId { get; set; }

    public string CredentialGuid { get; set; } = null!;

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    public string Guid { get; set; } = null!;

    public virtual PatientEntity Entity { get; set; } = null!;

    public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; } = new List<LaboratoryJobOrder>();

    public virtual ICollection<PatientAilment> PatientAilments { get; } = new List<PatientAilment>();

    public virtual ICollection<PatientConsultation> PatientConsultations { get; } = new List<PatientConsultation>();

    public virtual ICollection<PatientLaboratory> PatientLaboratories { get; } = new List<PatientLaboratory>();

    public virtual ICollection<PatientReminder> PatientReminders { get; } = new List<PatientReminder>();

    public virtual ICollection<PatientTag> PatientTags { get; } = new List<PatientTag>();

    public virtual ICollection<PharmacyJobOrder> PharmacyJobOrders { get; } = new List<PharmacyJobOrder>();
}
