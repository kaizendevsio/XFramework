using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

public class UpdateDoctorJobOrderRequest : RequestBase
{
    public TransactionStatus Status { get; set; }
    public TransactionStatus? PaymentStatus { get; set; }
    public DiscountType DiscountType { get; set; }
    public long ConsultationId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Remarks { get; set; }
    public decimal? AmountDue { get; set; }
    public decimal? AmountPaid { get; set; }
    public decimal? Discount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Prescription { get; set; }
    public string? Diagnosis { get; set; }
    public string? Treatment { get; set; }
    public string? Symptoms { get; set; }
}