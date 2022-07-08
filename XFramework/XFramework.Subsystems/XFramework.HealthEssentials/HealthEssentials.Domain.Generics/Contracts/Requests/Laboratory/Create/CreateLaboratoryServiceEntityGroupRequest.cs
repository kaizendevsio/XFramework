using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

public class CreateLaboratoryServiceEntityGroupRequest : RequestBase
{
    public string? Name { get; set; }

}