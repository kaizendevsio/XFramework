using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

public class UpdateDoctorIdentityRequest : RequestBase
{
    public GenericStatusType Status { get; set; }
}