using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Verify;

public class VerifyPatientRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}