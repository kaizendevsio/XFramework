namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryJobOrderDetail : BaseModel
{
    public Guid LaboratoryJobOrderId { get; set; }

    public Guid LaboratoryServiceId { get; set; }

    public string? Quantity { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }


    public virtual LaboratoryJobOrder LaboratoryJobOrder { get; set; } = null!;

    public virtual LaboratoryService LaboratoryService { get; set; } = null!;
}