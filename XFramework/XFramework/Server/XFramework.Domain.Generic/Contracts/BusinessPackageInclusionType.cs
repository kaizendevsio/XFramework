namespace XFramework.Domain.Generic.Contracts;

public partial record BusinessPackageInclusionType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool? IsNumericValue { get; set; }

    public string? Code { get; set; }

    public string? IconImage { get; set; }

    public string? Unit { get; set; }


    public virtual ICollection<BusinessPackageInclusion> BusinessPackageInclusions { get; } =
        new List<BusinessPackageInclusion>();
}