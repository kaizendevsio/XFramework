﻿using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

public class PatientConsultationResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long PatientId { get; set; }
    public long? ConsultationJobOrderId { get; set; }
    public string Guid { get; set; } = null!;

    public ConsultationJobOrder? ConsultationJobOrder { get; set; }
}