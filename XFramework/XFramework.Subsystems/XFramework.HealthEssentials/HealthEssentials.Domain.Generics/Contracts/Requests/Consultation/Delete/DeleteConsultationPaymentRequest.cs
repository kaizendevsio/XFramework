using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Delete;

public class DeleteConsultationPaymentRequest : RequestBase
{
    public Guid? Guid { get; set; }
}