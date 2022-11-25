namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Update;

public class UpdateConsultationJobOrderRequest : RequestBase
{
    public Guid? ConsultationGuid { get; set; }
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
    public string? Prescription { get; set; }
    public string? Diagnosis { get; set; }
    public string? Treatment { get; set; }
    public string? Symptoms { get; set; }
    public string? MeetingLink { get; set; }
    public Guid? ScheduleGuid { get; set; }
}