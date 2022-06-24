using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class UpdateLaboratoryServiceEntityGroupRequest : RequestBase
{
    public string? Name { get; set; }
}