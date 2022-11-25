using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class Medicine
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long EntityId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? ScientificName { get; set; }

    public string? Description { get; set; }

    public string? ChemicalComponent { get; set; }

    public string Guid { get; set; } = null!;

    public virtual ICollection<ConsultationJobOrderMedicine> ConsultationJobOrderMedicines { get; } = new List<ConsultationJobOrderMedicine>();

    public virtual MedicineEntity Entity { get; set; } = null!;

    public virtual ICollection<MedicineTag> MedicineTags { get; } = new List<MedicineTag>();

    public virtual ICollection<MedicineVendor> MedicineVendors { get; } = new List<MedicineVendor>();

    public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicines { get; } = new List<PharmacyJobOrderMedicine>();

    public virtual ICollection<PharmacyStock> PharmacyStocks { get; } = new List<PharmacyStock>();
}
