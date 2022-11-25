using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class Consultation
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

    public virtual ICollection<ConsultationJobOrder> ConsultationJobOrders { get; } = new List<ConsultationJobOrder>();

    public virtual ICollection<ConsultationTag> ConsultationTags { get; } = new List<ConsultationTag>();

    public virtual ICollection<DoctorConsultation> DoctorConsultations { get; } = new List<DoctorConsultation>();

    public virtual ConsultationEntity Entity { get; set; } = null!;

    public virtual ICollection<HospitalConsultation> HospitalConsultations { get; } = new List<HospitalConsultation>();
}
