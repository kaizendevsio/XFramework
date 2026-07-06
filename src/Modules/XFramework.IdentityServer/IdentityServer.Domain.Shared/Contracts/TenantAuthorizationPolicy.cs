namespace IdentityServer.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class TenantAuthorizationPolicy : BaseModel
{
    [MemoryPackOrder(0)]
    public MissingPermissionBehavior MissingPermissionBehavior { get; set; } = MissingPermissionBehavior.Deny;

    [MemoryPackOrder(1)]
    public virtual Tenant Tenant { get; set; } = null!;
}
