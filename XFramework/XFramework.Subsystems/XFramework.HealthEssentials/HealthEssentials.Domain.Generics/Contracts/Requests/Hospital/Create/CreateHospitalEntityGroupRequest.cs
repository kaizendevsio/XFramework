using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

public class CreateHospitalEntityGroupRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}