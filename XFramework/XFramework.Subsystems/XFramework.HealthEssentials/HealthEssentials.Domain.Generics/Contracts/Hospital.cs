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

    public virtual ICollection<HospitalConsultation> HospitalConsultations { get; set; } = new List<HospitalConsultation>();

    public virtual ICollection<HospitalLaboratory> HospitalLaboratories { get; set; } = new List<HospitalLaboratory>();

    public virtual ICollection<HospitalLocation> HospitalLocations { get; set; } = new List<HospitalLocation>();

    public virtual ICollection<HospitalService> HospitalServices { get; set; } = new List<HospitalService>();

    public virtual ICollection<HospitalTag> HospitalTags { get; set; } = new List<HospitalTag>();
}