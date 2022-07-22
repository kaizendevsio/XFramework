namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

public class UpdateHospitalEntityGroupRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}