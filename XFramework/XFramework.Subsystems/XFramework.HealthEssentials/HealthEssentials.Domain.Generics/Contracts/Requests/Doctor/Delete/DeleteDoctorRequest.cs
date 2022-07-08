using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Delete;

public class DeleteDoctorRequest : RequestBase
{
    public Guid? Guid { get; set; }
}