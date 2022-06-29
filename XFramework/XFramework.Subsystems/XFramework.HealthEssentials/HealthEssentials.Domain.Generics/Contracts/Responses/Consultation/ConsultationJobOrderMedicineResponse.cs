

using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

public class ConsultationJobOrderMedicineResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
    public Guid? Guid { get; set; }
    
    public ConsultationJobOrderResponse? ConsultationJobOrder { get; set; }
    public MedicineResponse? Medicine { get; set; }
    public MedicineIntakeResponse? MedicineIntake { get; set; }
}