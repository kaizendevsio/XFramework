using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

public class UpdateDoctorRequest : RequestBase
{
    public GenericStatusType Status { get; set; }
}