﻿namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyJobOrder : BaseModel
{
    public Guid PharmacyBranchId { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }

    public short? PaymentStatus { get; set; }

    public Guid WalletTypeId { get; set; }

    public decimal? AmountDue { get; set; }

    public decimal? AmountPaid { get; set; }

    public decimal? Discount { get; set; }

    public decimal? DiscountType { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? PrescriptionNote { get; set; }
    
    public Guid UnavailabilityHandling { get; set; }
    
    public Guid ScheduleId { get; set; }

    public Guid PatientId { get; set; }

    public virtual Patient? Patient { get; set; }

    public virtual ICollection<PharmacyJobOrderConsultationJobOrder> PharmacyJobOrderConsultationJobOrders { get; set; } =
        new List<PharmacyJobOrderConsultationJobOrder>();

    public virtual ICollection<PharmacyJobOrderMedicine> PharmacyJobOrderMedicines { get; set; } =
        new List<PharmacyJobOrderMedicine>();

    public virtual PharmacyBranch PharmacyBranch { get; set; } = null!;

    public virtual Schedule Schedule { get; set; } = null!;
}