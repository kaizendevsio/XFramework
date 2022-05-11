﻿using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

public class ConsultationJobOrderMedicineResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long ConsultationJobOrderId { get; set; }
    public long? MedicineId { get; set; }
    public long? MedicineIntakeId { get; set; }
    public string? Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
    public string Guid { get; set; } = null!;
    
    public ConsultationJobOrder ConsultationJobOrder { get; set; } = null!;
}