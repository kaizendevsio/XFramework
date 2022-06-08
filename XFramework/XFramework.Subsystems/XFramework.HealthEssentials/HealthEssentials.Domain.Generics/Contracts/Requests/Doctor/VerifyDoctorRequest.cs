using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

public class VerifyDoctorRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}