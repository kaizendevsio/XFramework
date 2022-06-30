

using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

public class LogisticJobOrderDetailResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public short Status { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public long Quantity { get; set; }
    public Guid? Guid { get; set; }
    public long UnitPrice { get; set; }
    public long Discount { get; set; }
    public long DiscountType { get; set; }
    public string LocationGuid { get; set; } = null!;
    
    public LogisticJobOrderResponse LogisticJobOrder { get; set; } = null!;
    public UnitResponse? Unit { get; set; }
}