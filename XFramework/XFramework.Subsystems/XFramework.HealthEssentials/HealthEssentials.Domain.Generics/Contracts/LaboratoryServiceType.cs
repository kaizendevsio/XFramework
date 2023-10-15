namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryServiceType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual ICollection<ConsultationJobOrderLaboratory> ConsultationJobOrderLaboratories { get; } =
        new List<ConsultationJobOrderLaboratory>();

    public virtual LaboratoryServiceTypeGroup Group { get; set; } = null!;

    public virtual ICollection<LaboratoryService> LaboratoryServices { get; } = new List<LaboratoryService>();
}