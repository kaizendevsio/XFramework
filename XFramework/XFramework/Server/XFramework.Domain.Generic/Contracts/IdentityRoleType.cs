namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityRoleType
{
    public Guid Id { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public string? Name { get; set; }

    public short? RoleLevel { get; set; }

    public string? Guid { get; set; }

    public long ApplicationId { get; set; }

    public long? GroupId { get; set; }

    public virtual Application Application { get; set; } = null!;

    public virtual IdentityRoleTypeGroup? Group { get; set; }

    public virtual ICollection<IdentityRole> IdentityRoles { get; } = new List<IdentityRole>();
}
