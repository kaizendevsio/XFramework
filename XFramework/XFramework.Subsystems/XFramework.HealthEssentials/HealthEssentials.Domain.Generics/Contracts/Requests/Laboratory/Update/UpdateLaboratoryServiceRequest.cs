using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryServiceRequest : RequestBase
{
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public Guid? EntityGuid { get; set; }

    public decimal Price { get; set; }
    public decimal MaxDiscount { get; set; }
    public decimal Quantity { get; set; }
    public Guid? UnitGuid { get; set; }
}