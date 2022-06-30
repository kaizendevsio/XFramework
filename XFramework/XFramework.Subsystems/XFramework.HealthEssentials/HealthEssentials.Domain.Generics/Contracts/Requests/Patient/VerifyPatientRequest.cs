using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient;

public class VerifyPatientRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}