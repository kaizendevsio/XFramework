namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Get;

public class GetDoctorConsultationListRequest : QueryableRequest
{
    public Guid? DoctorGuid { get; set; }
}