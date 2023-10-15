namespace XFramework.Domain.Generic.Contracts;

public partial record IdentityRoleType : BaseModel
{
    public string? Name { get; set; }

    public short? RoleLevel { get; set; }

    public Guid TenantId { get; set; }

    public Guid GroupId { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual IdentityRoleTypeGroup? Group { get; set; }

    public virtual ICollection<IdentityRole> IdentityRoles { get; } = new List<IdentityRole>();
}