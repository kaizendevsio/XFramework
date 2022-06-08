using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class CreateLaboratoryServiceEntityGroupRequest : RequestBase
{
    public string? Name { get; set; }

}