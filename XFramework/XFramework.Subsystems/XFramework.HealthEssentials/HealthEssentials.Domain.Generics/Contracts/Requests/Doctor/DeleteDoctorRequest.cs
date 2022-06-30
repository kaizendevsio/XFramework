using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

public class DeleteDoctorRequest : RequestBase
{
    public Guid? Guid { get; set; }
}