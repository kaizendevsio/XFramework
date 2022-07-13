namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryLocationTagRequest : RequestBase
{
    public Guid LaboratoryLocationGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}