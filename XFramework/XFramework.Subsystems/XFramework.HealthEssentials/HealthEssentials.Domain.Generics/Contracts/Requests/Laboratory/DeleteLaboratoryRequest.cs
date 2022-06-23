using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class DeleteLaboratoryRequest : RequestBase
{
    public Guid? Guid { get; set; }
}