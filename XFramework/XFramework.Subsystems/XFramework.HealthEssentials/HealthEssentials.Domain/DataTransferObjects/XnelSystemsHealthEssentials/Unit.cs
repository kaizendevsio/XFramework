using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class Unit
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long EntityId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }

    public string Guid { get; set; } = null!;

    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicineDosageUnits { get; } = new List<ConsultationJobOrderMedicine>();

    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicineDurationUnits { get; } = new List<ConsultationJobOrderMedicine>();

    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicineIntakeUnits { get; } = new List<ConsultationJobOrderMedicine>();

    public virtual UnitEntity Entity { get; set; } = null!;

    public virtual ICollection<HospitalService> HospitalServices { get; } = new List<HospitalService>();

    public virtual ICollection<LaboratoryService> LaboratoryServices { get; } = new List<LaboratoryService>();

    public virtual ICollection<LogisticJobOrderDetail> LogisticJobOrderDetails { get; } = new List<LogisticJobOrderDetail>();

    public virtual ICollection<MedicineIntake> MedicineIntakes { get; } = new List<MedicineIntake>();

    public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicineDosageUnits { get; } = new List<PharmacyJobOrderMedicine>();

    public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicineDurationUnits { get; } = new List<PharmacyJobOrderMedicine>();

    public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicineIntakeUnits { get; } = new List<PharmacyJobOrderMedicine>();

    public virtual ICollection<PharmacyService> PharmacyServices { get; } = new List<PharmacyService>();

    public virtual ICollection<PharmacyStock> PharmacyStocks { get; } = new List<PharmacyStock>();
}
