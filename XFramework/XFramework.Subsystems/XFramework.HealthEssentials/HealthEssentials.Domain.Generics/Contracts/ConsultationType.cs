namespace HealthEssentials.Domain.Generics.Contracts;

public partial class ConsultationType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual ICollection<Consultation> Consultations { get; set; } = new List<Consultation>();

    public virtual ConsultationTypeGroup Group { get; set; } = null!;
}