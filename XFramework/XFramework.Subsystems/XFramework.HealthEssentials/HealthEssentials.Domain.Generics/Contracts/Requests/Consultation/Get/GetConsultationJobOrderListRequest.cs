using HealthEssentials.Domain.Generics.Enums;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Get;

public class GetConsultationJobOrderListRequest : QueryableRequest
{
    public TransactionStatus Status { get; set; }
}