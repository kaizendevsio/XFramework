using HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

public class CreateLaboratoryJobOrderRequest : RequestBase
{
    public Guid? ConsultationJobOrderGuid { get; set; }
    public Guid? PatientGuid { get; set; }
    public Guid? LaboratoryLocationGuid { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Remarks { get; set; }
    public TransactionStatus Status { get; set; }
    public TransactionStatus PaymentStatus { get; set; }
    public Guid? WalletTypeGuid { get; set; }
    public decimal? AmountDue { get; set; }
    public decimal? AmountPaid { get; set; }
    public decimal? Discount { get; set; }
    public DiscountType DiscountType { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? ScheduleGuid { get; set; }

    public List<LaboratoryJobOrderDetailResponse> DetailList { get; set; }
}