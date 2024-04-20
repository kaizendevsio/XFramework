using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class BusinessPackageInclusionType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool? IsNumericValue { get; set; }

    public string? Code { get; set; }

    public string? IconImage { get; set; }

    public string? Unit { get; set; }


    public virtual ICollection<BusinessPackageInclusion> BusinessPackageInclusions { get; set; } =
        new List<BusinessPackageInclusion>();
}