﻿using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class LaboratoryJobOrder
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long LaboratoryLocationId { get; set; }

    public long LaboratoryId { get; set; }

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

    public string Guid { get; set; } = null!;

    public long ScheduleId { get; set; }

    public long? ConsultationJobOrderId { get; set; }

    public long? PatientId { get; set; }

    public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }

    public virtual Laboratory Laboratory { get; set; } = null!;

    public virtual ICollection<LaboratoryJobOrderDetail> LaboratoryJobOrderDetails { get; } = new List<LaboratoryJobOrderDetail>();

    public virtual ICollection<LaboratoryJobOrderResult> LaboratoryJobOrderResults { get; } = new List<LaboratoryJobOrderResult>();

    public virtual LaboratoryLocation LaboratoryLocation { get; set; } = null!;

    public virtual Patient? Patient { get; set; }

    public virtual ICollection<PatientLaboratory> PatientLaboratories { get; } = new List<PatientLaboratory>();

    public virtual Schedule Schedule { get; set; } = null!;
}
