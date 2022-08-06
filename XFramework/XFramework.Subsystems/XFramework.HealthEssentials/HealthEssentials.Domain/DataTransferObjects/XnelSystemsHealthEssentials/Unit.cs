using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class Unit
    {
        public Unit()
        {
            ConsultationJobOrderMedicineDosageUnits = new HashSet<ConsultationJobOrderMedicine>();
            ConsultationJobOrderMedicineDurationUnits = new HashSet<ConsultationJobOrderMedicine>();
            ConsultationJobOrderMedicineIntakeUnits = new HashSet<ConsultationJobOrderMedicine>();
            HospitalServices = new HashSet<HospitalService>();
            LaboratoryServices = new HashSet<LaboratoryService>();
            LogisticJobOrderDetails = new HashSet<LogisticJobOrderDetail>();
            MedicineIntakes = new HashSet<MedicineIntake>();
            PharmacyServices = new HashSet<PharmacyService>();
            PharmacyStocks = new HashSet<PharmacyStock>();
        }

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

        public virtual UnitEntity Entity { get; set; } = null!;
        public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicineDosageUnits { get; set; }
        public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicineDurationUnits { get; set; }
        public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicineIntakeUnits { get; set; }
        public virtual ICollection<HospitalService> HospitalServices { get; set; }
        public virtual ICollection<LaboratoryService> LaboratoryServices { get; set; }
        public virtual ICollection<LogisticJobOrderDetail> LogisticJobOrderDetails { get; set; }
        public virtual ICollection<MedicineIntake> MedicineIntakes { get; set; }
        public virtual ICollection<PharmacyService> PharmacyServices { get; set; }
        public virtual ICollection<PharmacyStock> PharmacyStocks { get; set; }
    }
}
