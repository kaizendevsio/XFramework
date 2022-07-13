namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

public class CreateLaboratoryTagRequest : RequestBase
{
    public Guid LaboratoryGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}