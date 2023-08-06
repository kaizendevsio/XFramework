namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Tag
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }

    
    public virtual ICollection<AilmentTag> AilmentTags { get; } = new List<AilmentTag>();

    public virtual ICollection<ConsultationTag> ConsultationTags { get; } = new List<ConsultationTag>();

    public virtual ICollection<DoctorTag> DoctorTags { get; } = new List<DoctorTag>();

    public virtual TagType Type { get; set; } = null!;

    public virtual ICollection<HospitalServiceTag> HospitalServiceTags { get; } = new List<HospitalServiceTag>();

    public virtual ICollection<HospitalTag> HospitalTags { get; } = new List<HospitalTag>();

    public virtual ICollection<LaboratoryLocationTag> LaboratoryLocationTags { get; } = new List<LaboratoryLocationTag>();

    public virtual ICollection<LaboratoryServiceTag> LaboratoryServiceTags { get; } = new List<LaboratoryServiceTag>();

    public virtual ICollection<LaboratoryTag> LaboratoryTags { get; } = new List<LaboratoryTag>();

    public virtual ICollection<LogisticRiderTag> LogisticRiderTags { get; } = new List<LogisticRiderTag>();

    public virtual ICollection<MedicineTag> MedicineTags { get; } = new List<MedicineTag>();

    public virtual ICollection<PatientTag> PatientTags { get; } = new List<PatientTag>();

    public virtual ICollection<PharmacyServiceTag> PharmacyServiceTags { get; } = new List<PharmacyServiceTag>();

    public virtual ICollection<PharmacyTag> PharmacyTags { get; } = new List<PharmacyTag>();

    public virtual ICollection<ScheduleTag> ScheduleTags { get; } = new List<ScheduleTag>();
}
