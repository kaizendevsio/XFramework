using HealthEssentials.Domain.Generics.Enums;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

public class GetDoctorJobOrderListRequest : QueryableRequest
{
    public TransactionRecordType RecordType { get; set; }
    public Guid? DoctorGuid { get; set; }
}