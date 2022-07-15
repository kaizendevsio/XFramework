namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

public class CreateHospitalLaboratoryRequest : RequestBase
{
    public Guid? HospitalGuid { get; set; }
    public Guid? LaboratoryGuid { get; set; }
}