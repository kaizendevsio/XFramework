namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;

public class UpdateLogisticJobOrderDetailRequest : RequestBase
{
    public Guid LogisticJobOrderGuid { get; set; }
    public short Status { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public long Quantity { get; set; }
    public Guid? UnitGuid { get; set; }
    public long UnitPrice { get; set; }
    public long Discount { get; set; }
    public long DiscountType { get; set; }
    public Guid? LocationGuid { get; set; }
}