using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital;

public class CreateHospitalEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}