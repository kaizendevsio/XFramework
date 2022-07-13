namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

public class UpdateHospitalConsultationRequest : RequestBase
{
    public Guid HospitalGuid { get; set; }
    public Guid? ConsultationGuid { get; set; }
}