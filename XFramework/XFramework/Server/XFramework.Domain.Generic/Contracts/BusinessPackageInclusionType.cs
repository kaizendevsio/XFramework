namespace XFramework.Domain.Generic.Contracts;

public partial class BusinessPackageInclusionType
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public DateTime? LastChanged { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool? IsNumericValue { get; set; }

    public string? Code { get; set; }

    public string? IconImage { get; set; }

    public string? Unit { get; set; }

    
    public virtual ICollection<BusinessPackageInclusion> BusinessPackageInclusions { get; } = new List<BusinessPackageInclusion>();
}
