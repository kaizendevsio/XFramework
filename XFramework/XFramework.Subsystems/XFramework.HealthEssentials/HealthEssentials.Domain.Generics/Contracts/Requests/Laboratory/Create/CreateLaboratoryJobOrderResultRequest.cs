namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

public class CreateLaboratoryJobOrderResultRequest : RequestBase
{
    public Guid LaboratoryJobOrderGuid { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
}