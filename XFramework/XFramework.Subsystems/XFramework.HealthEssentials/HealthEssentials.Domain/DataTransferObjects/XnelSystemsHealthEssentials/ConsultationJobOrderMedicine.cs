using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class ConsultationJobOrderMedicine
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long ConsultationJobOrderId { get; set; }

    public long? MedicineId { get; set; }

    public long? MedicineIntakeId { get; set; }

    public int Quantity { get; set; }

    public string? PrescriptionNote { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }

    public string Guid { get; set; } = null!;

    public int IntakeRepetition { get; set; }

    public long IntakeUnitId { get; set; }

    public decimal Duration { get; set; }

    public long DurationUnitId { get; set; }

    public int? Dosage { get; set; }

    public long? DosageUnitId { get; set; }

    public virtual ConsultationJobOrder ConsultationJobOrder { get; set; } = null!;

    public virtual Unit? DosageUnit { get; set; }

    public virtual Unit DurationUnit { get; set; } = null!;

    public virtual Unit IntakeUnit { get; set; } = null!;

    public virtual Medicine? Medicine { get; set; }

    public virtual MedicineIntake? MedicineIntake { get; set; }
}
