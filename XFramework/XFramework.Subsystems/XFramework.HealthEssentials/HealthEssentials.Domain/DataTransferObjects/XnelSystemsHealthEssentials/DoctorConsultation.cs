using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class DoctorConsultation
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long DoctorId { get; set; }

    public long? ConsultationId { get; set; }

    public string Guid { get; set; } = null!;

    public decimal? Price { get; set; }

    public decimal? MaxDiscount { get; set; }

    public int Quantity { get; set; }

    public virtual Consultation? Consultation { get; set; }

    public virtual Doctor Doctor { get; set; } = null!;
}
