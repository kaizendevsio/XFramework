namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Hospital : BaseModel
{
    public Guid TypeId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Remarks { get; set; }


    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? Logo { get; set; }

    public virtual HospitalType Type { get; set; } = null!;

    public virtual ICollection<HospitalConsultation> HospitalConsultations { get; } = new List<HospitalConsultation>();

    public virtual ICollection<HospitalLaboratory> HospitalLaboratories { get; } = new List<HospitalLaboratory>();

    public virtual ICollection<HospitalLocation> HospitalLocations { get; } = new List<HospitalLocation>();

    public virtual ICollection<HospitalService> HospitalServices { get; } = new List<HospitalService>();

    public virtual ICollection<HospitalTag> HospitalTags { get; } = new List<HospitalTag>();
}