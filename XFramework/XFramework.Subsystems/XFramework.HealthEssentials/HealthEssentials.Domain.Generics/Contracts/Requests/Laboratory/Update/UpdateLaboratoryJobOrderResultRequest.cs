namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryJobOrderResultRequest : RequestBase
{
    public Guid LaboratoryJobOrderGuid { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
}