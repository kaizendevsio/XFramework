using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyJobOrderMedicineResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    public TransactionStatus? Status { get; set; }
    public Guid? Guid { get; set; }

    public PharmacyJobOrderResponse? PharmacyJobOrder { get; set; }
    public MedicineIntakeResponse? MedicineIntake { get; set; }
    public MedicineResponse? Medicine { get; set; }

}