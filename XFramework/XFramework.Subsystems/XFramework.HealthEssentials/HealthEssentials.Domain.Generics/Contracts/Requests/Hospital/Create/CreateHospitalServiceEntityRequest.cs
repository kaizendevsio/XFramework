namespace HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

public class CreateHospitalServiceEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid GroupGuid { get; set; }
    public int? SortOrder { get; set; }
}