using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class AddSupportedLaboratoryServiceRequest : RequestBase
{
    public Guid? ServiceGuid { get; set; }
    public Guid? LaboratoryGuid { get; set; }
    
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? Quantity { get; set; }
}