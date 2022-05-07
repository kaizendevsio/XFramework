using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;

public class CreateConsultationPaymentRequest : TransactionRequestBase
{
    public Guid? PatientGuid { get; set; }
    public Guid? DoctorGuid { get; set; }
    public Guid? ConsultationGuid { get; set; }
    public decimal Amount { get; set; }
}