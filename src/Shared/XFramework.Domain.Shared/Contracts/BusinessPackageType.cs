using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class BusinessPackageType : BaseModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<BusinessPackage> BusinessPackages { get; set; } = new List<BusinessPackage>();
}