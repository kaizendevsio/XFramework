namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

public class UpdateHospitalLaboratoryRequest : RequestBase
{
    public Guid HospitalGuid { get; set; }
    public Guid? LaboratoryGuid { get; set; }
}