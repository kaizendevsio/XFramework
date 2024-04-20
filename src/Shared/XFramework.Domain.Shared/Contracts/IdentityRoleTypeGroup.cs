using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class IdentityRoleTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;


    public virtual ICollection<IdentityRoleType> IdentityRoleTypes { get; set; } = new List<IdentityRoleType>();
}