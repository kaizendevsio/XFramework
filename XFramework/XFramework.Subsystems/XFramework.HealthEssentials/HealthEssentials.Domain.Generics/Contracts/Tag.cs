namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Tag : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }


    public virtual ICollection<AilmentTag> AilmentTags { get; set; } = new List<AilmentTag>();

    public virtual ICollection<ConsultationTag> ConsultationTags { get; set; } = new List<ConsultationTag>();

    public virtual ICollection<DoctorTag> DoctorTags { get; set; } = new List<DoctorTag>();

    public virtual TagType Type { get; set; } = null!;

    public virtual ICollection<HospitalServiceTag> HospitalServiceTags { get; set; } = new List<HospitalServiceTag>();

    public virtual ICollection<HospitalTag> HospitalTags { get; set; } = new List<HospitalTag>();

    public virtual ICollection<LaboratoryLocationTag> LaboratoryLocationTags { get; set; } =
        new List<LaboratoryLocationTag>();

    public virtual ICollection<LaboratoryServiceTag> LaboratoryServiceTags { get; set; } = new List<LaboratoryServiceTag>();

    public virtual ICollection<LaboratoryTag> LaboratoryTags { get; set; } = new List<LaboratoryTag>();

    public virtual ICollection<LogisticRiderTag> LogisticRiderTags { get; set; } = new List<LogisticRiderTag>();

    public virtual ICollection<MedicineTag> MedicineTags { get; set; } = new List<MedicineTag>();

    public virtual ICollection<PatientTag> PatientTags { get; set; } = new List<PatientTag>();

    public virtual ICollection<PharmacyServiceTag> PharmacyServiceTags { get; set; } = new List<PharmacyServiceTag>();

    public virtual ICollection<PharmacyTag> PharmacyTags { get; set; } = new List<PharmacyTag>();

    public virtual ICollection<ScheduleTag> ScheduleTags { get; set; } = new List<ScheduleTag>();
}