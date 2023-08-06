namespace HealthEssentials.Domain.Generics.Contracts;

public partial class ConsultationJobOrderLaboratory
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid ConsultationJobOrderId { get; set; }

    public Guid LaboratoryServiceId { get; set; }

    public Guid SuggestedLaboratoryLocationId { get; set; }

    public string? Quantity { get; set; }

    public string? PrescriptionNote { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }

    
    public virtual ConsultationJobOrder ConsultationJobOrder { get; set; } = null!;

    public virtual LaboratoryServiceType LaboratoryService { get; set; } = null!;

    public virtual LaboratoryLocation? SuggestedLaboratoryLocation { get; set; }
}
