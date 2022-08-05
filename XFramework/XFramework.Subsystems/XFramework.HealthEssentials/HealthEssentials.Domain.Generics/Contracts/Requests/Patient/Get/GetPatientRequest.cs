using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;

public class GetPatientRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}