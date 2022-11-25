namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

public class CreateLaboratoryServiceTagRequest : RequestBase
{
    public Guid? LaboratoryServiceGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}