using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

public class PatientLaboratoryResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long PatientId { get; set; }
    public long? LaboratoryJobOrderId { get; set; }
    public string Guid { get; set; } = null!;

    public LaboratoryJobOrder? LaboratoryJobOrder { get; set; }
}