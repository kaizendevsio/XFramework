﻿namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Create;

public class CreateConsultationJobOrderLaboratoryRequest : RequestBase
{
    public Guid? ConsultationJobOrderGuid { get; set; }
    public Guid? LaboratoryServiceGuid { get; set; }
    public Guid? SuggestedLaboratoryLocationGuid { get; set; }
    
    public string? Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
}