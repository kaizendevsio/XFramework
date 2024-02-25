namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryService : BaseModel
{
    public Guid TypeId { get; set; }

    public Guid LaboratoryBranchId { get; set; }

    public Guid LaboratoryId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public decimal? MaxDiscount { get; set; }

    public decimal? Quantity { get; set; }

    public Guid UnitId { get; set; }


    public virtual LaboratoryServiceType Type { get; set; } = null!;

    public virtual Laboratory Laboratory { get; set; } = null!;

    public virtual ICollection<LaboratoryJobOrderDetail> LaboratoryJobOrderDetails { get; set; } =
        new List<LaboratoryJobOrderDetail>();

    public virtual LaboratoryBranch LaboratoryBranch { get; set; } = null!;

    public virtual ICollection<LaboratoryServiceTag> LaboratoryServiceTags { get; set; } = new List<LaboratoryServiceTag>();

    public virtual Unit? Unit { get; set; }
}