namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryJobOrderResult : BaseModel
{
    public Guid LaboratoryJobOrderId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }


    public virtual LaboratoryJobOrder LaboratoryJobOrder { get; set; } = null!;

    public virtual ICollection<LaboratoryJobOrderResultFile> LaboratoryJobOrderResultFiles { get; } =
        new List<LaboratoryJobOrderResultFile>();
}