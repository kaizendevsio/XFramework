namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;

public class CreateConsultationJobOrderMedicineRequest : RequestBase
{
    public Guid? ConsultationJobOrderGuid { get; set; }
    public Guid? MedicineGuid { get; set; }
    public Guid? MedicineIntakeGuid { get; set; }
    public string? Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
}