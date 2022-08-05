using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;

public class DeletePatientRequest : RequestBase
{
    public Guid? Guid { get; set; }
}