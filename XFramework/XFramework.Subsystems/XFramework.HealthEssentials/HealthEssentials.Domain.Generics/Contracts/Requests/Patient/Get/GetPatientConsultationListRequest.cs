using HealthEssentials.Domain.Generics.Enums;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;

public class GetPatientConsultationListRequest : QueryableRequest
{
    public TransactionRecordType Status { get; set; }

}