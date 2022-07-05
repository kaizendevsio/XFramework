using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Delete;

public class DeleteLaboratoryServiceRequest : RequestBase
{
    public Guid? Guid { get; set; }
}