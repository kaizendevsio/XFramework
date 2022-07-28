using HealthEssentials.Domain.Generics.Enums;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Get;

public class GetDoctorConsultationJobOrderListRequest : QueryableRequest
{
    public TransactionRecordType Status { get; set; }
    public Guid? DoctorGuid { get; set; }
}