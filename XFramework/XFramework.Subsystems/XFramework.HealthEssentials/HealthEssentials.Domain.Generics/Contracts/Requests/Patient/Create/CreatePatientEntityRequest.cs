namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

public class CreatePatientEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? GroupGuid { get; set; }
    public int? SortOrder { get; set; }
}