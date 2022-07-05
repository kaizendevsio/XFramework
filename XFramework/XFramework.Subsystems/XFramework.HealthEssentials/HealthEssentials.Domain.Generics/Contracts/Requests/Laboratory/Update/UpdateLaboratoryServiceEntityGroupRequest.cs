using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryServiceEntityGroupRequest : RequestBase
{
    public string? Name { get; set; }
}