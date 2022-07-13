namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

public class CreateLaboratoryLocationTagRequest : RequestBase
{
    public Guid LaboratoryLocationGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}