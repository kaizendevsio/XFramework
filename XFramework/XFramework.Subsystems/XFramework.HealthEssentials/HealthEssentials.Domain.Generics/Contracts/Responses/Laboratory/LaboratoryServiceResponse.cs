using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Laboratory;

public class LaboratoryServiceResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal? Quantity { get; set; }
    public Guid? Guid { get; set; }

    public LaboratoryServiceEntityGroupResponse? Entity { get; set; }
    public LaboratoryLocationResponse? LaboratoryLocation { get; set; }
    public LaboratoryResponse? Laboratory { get; set; }
    public UnitResponse? Unit { get; set; }
}