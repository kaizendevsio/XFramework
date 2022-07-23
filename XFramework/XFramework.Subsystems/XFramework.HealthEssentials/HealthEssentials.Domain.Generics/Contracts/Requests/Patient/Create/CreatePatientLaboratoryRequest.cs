namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

public class CreatePatientLaboratoryRequest : RequestBase
{
    public Guid? PatientGuid { get; set; }
    public Guid? LaboratoryJobOrderGuid { get; set; }
}