

using HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

public class ConsultationJobOrderLaboratoryResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long? SuggestedLaboratoryLocationId { get; set; }
    public string? Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
    public Guid? Guid { get; set; }
    
    public ConsultationJobOrderResponse? ConsultationJobOrder { get; set; }
    public LaboratoryServiceResponse? LaboratoryService { get; set; }
}