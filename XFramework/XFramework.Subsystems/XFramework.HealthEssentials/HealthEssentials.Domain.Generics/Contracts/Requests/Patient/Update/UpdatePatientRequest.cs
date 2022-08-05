using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;

public class UpdatePatientRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? EntityGuid { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
}