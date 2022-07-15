namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryTagRequest : RequestBase
{
    public Guid? LaboratoryGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}