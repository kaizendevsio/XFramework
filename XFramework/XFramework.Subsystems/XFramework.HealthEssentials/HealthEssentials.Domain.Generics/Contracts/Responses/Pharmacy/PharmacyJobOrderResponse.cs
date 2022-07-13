﻿using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyJobOrderResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
    public short? PaymentStatus { get; set; }
    public long? WalletTypeId { get; set; }
    public decimal? AmountDue { get; set; }
    public decimal? AmountPaid { get; set; }
    public decimal? Discount { get; set; }
    public decimal? DiscountType { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? PrescriptionNote { get; set; }
    public Guid? Guid { get; set; }

    public PatientResponse? Patient { get; set; }
    public PharmacyLocationResponse? PharmacyLocation { get; set; }
    public ScheduleResponse? Schedule { get; set; }
    public List<PharmacyJobOrderConsultationJobOrderResponse>? PharmacyJobOrderConsultationJobOrders { get; set; }
    public List<PharmacyJobOrderMedicineResponse>? PharmacyJobOrderMedicines { get; set; }
}