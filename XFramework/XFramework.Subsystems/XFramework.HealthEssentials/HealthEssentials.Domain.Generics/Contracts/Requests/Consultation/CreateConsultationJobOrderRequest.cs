using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;

public class CreateConsultationJobOrderRequest : RequestBase
{
    public Guid? ConsultationGuid { get; set; }
    public int ReferenceNumber { get; set; }
    public string? Remarks { get; set; }
    public string? PaymentStatus { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime AmountDue { get; set; }
    public int AmountPaid { get; set; }
    public string? Diagnosis { get; set; }
    public string? Treatment { get; set; }
    public string? Symptoms { get; set; }
    public Guid? ScheduleGuid { get; set; }
}