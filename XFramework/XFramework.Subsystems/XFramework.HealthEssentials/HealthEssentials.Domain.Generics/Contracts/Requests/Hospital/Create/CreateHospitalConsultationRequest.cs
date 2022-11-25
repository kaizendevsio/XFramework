namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

public class CreateHospitalConsultationRequest : RequestBase
{
    public Guid? HospitalGuid { get; set; }
    public Guid? ConsultationGuid { get; set; }
}