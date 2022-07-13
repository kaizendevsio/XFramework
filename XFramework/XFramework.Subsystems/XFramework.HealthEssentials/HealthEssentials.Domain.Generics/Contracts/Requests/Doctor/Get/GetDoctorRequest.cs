using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Get;

public class GetDoctorRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}