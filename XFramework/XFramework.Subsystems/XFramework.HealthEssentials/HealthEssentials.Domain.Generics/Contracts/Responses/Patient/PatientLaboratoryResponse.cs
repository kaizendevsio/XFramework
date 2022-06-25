

using HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

public class PatientLaboratoryResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long PatientId { get; set; }
    public string Guid { get; set; } = null!;

    public LaboratoryJobOrderResponse? LaboratoryJobOrder { get; set; }
    public PatientResponse? Patient { get; set; }
}