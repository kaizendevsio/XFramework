using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyJobOrderMedicineResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public int Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    public TransactionStatus? Status { get; set; }
    public Guid? Guid { get; set; }
    public int IntakeRepetition { get; set; }
    public decimal Duration { get; set; }
    public int? Dosage { get; set; }

    public PharmacyJobOrderResponse? PharmacyJobOrder { get; set; }
    public MedicineIntakeResponse? MedicineIntake { get; set; }
    public MedicineResponse? Medicine { get; set; }
    public UnitResponse? IntakeUnit { get; set; }
    public UnitResponse? DurationUnit { get; set; }
    public UnitResponse? DosageUnit { get; set; }

}