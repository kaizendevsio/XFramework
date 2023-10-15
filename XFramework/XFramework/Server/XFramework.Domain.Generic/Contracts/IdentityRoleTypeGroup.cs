namespace XFramework.Domain.Generic.Contracts;

public partial record IdentityRoleTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;


    public virtual ICollection<IdentityRoleType> IdentityRoleTypes { get; } = new List<IdentityRoleType>();
}