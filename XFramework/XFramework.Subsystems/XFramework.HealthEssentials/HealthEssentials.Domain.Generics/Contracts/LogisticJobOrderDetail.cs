namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LogisticJobOrderDetail : BaseModel
{
    public Guid LogisticJobOrderId { get; set; }

    public short Status { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int Quantity { get; set; }

    public Guid UnitId { get; set; }


    public decimal UnitPrice { get; set; }

    public Guid Discount { get; set; }

    public Guid DiscountType { get; set; }

    public Guid LocationGuid { get; set; }

    public virtual LogisticJobOrder LogisticJobOrder { get; set; } = null!;

    public virtual Unit? Unit { get; set; }
}