namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryJobOrderResponse
{
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
}