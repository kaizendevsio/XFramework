using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

public class CreateConsultationLaboratoryTestRequest : RequestBase
{
    public Guid? ConsultationJobOrderGuid { get; set; }
    public Guid? LaboratoryServiceGuid { get; set; }
    public Guid? SuggestedLaboratoryLocation { get; set; }

    public string? Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
}