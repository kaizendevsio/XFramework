namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PatientType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual PatientTypeGroup Group { get; set; } = null!;

    public virtual ICollection<Patient> Patients { get; set; } = new List<Patient>();
}