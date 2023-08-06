namespace HealthEssentials.Domain.Generics.Contracts;

public partial class DoctorType
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    
    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual ICollection<Doctor> Doctors { get; } = new List<Doctor>();

    public virtual DoctorTypeGroup Group { get; set; } = null!;
}
