namespace HealthEssentials.Domain.Generics.Contracts;

public partial class DoctorType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();

    public virtual DoctorTypeGroup Group { get; set; } = null!;
}