﻿using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

public class CreateMedicinePrescriptionRequest : RequestBase
{
    public Guid? ConsultationJobOrderGuid { get; set; }
    public string? MedicineName { get; set; }
    public Guid? MedicineGuid { get; set; }
    public long? MedicineIntakeGuid { get; set; }
    public string? Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    
}