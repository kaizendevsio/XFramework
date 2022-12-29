using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryJobOrderResponse
{
    public string TempAddress { get; set; }
    public string TempDocName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
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
    public Guid? Guid { get; set; }
    
    public virtual ConsultationJobOrderResponse? ConsultationJobOrder { get; set; }
    public virtual LaboratoryResponse? Laboratory { get; set; }
    public virtual LaboratoryLocationResponse? LaboratoryLocation { get; set; }
    public virtual PatientResponse? Patient { get; set; }
    public virtual ScheduleResponse? Schedule { get; set; }
    public virtual ICollection<LaboratoryJobOrderDetailResponse>? LaboratoryJobOrderDetails { get; set; }
    public virtual ICollection<LaboratoryJobOrderResultResponse>? LaboratoryJobOrderResults { get; set; }
    public virtual ICollection<PatientLaboratoryResponse>? PatientLaboratories { get; set; }
}