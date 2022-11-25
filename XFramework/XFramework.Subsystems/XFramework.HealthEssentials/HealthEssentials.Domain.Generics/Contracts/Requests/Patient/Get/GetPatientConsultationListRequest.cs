using HealthEssentials.Domain.Generics.Enums;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;

public class GetPatientConsultationListRequest : QueryableRequest
{
    public TransactionStatus Status { get; set; }
    public Guid? PatientGuid { get; set; }

}