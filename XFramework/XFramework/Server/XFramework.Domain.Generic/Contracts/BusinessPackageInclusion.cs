namespace XFramework.Domain.Generic.Contracts;

public partial class BusinessPackageInclusion : BaseModel
{
    public Guid BusinessPackageId { get; set; }

    public Guid TypeId { get; set; }

    public decimal? Value { get; set; }

    public string? StringValue { get; set; }

    public virtual BusinessPackageInclusionType Type { get; set; } = null!;

    public virtual BusinessPackage BusinessPackage { get; set; } = null!;
}