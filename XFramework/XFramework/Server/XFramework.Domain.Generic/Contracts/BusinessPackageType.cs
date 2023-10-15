namespace XFramework.Domain.Generic.Contracts;

public partial record BusinessPackageType : BaseModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<BusinessPackage> BusinessPackages { get; } = new List<BusinessPackage>();
}