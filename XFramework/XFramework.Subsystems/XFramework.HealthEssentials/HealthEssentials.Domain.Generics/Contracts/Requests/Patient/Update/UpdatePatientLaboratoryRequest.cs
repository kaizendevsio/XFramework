namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;

public class UpdatePatientLaboratoryRequest : RequestBase
{
    public Guid? PatientGuid { get; set; }
    public Guid? LaboratoryJobOrderGuid { get; set; }
}