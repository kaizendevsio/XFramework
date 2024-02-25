namespace HealthEssentials.Domain.Generics.Contracts;

public partial class ConsultationJobOrderLaboratory : BaseModel
{
    public Guid ConsultationJobOrderId { get; set; }

    public Guid LaboratoryServiceId { get; set; }

    public Guid? SuggestedLaboratoryBranchId { get; set; }

    public string? Quantity { get; set; }

    public string? PrescriptionNote { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }


    public virtual ConsultationJobOrder ConsultationJobOrder { get; set; } = null!;

    public virtual LaboratoryServiceType LaboratoryService { get; set; } = null!;

    public virtual LaboratoryBranch? SuggestedLaboratoryLocation { get; set; }
}