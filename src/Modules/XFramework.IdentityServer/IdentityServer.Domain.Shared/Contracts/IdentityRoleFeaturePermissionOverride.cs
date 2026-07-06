namespace IdentityServer.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityRoleFeaturePermissionOverride : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid IdentityRoleId { get; set; }

    [MemoryPackOrder(1)]
    public string ModuleKey { get; set; } = null!;

    [MemoryPackOrder(2)]
    public string SubFeatureKey { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string CapabilityKey { get; set; } = null!;

    [MemoryPackOrder(4)]
    public RoleCapabilityPermissionEffect Effect { get; set; } = RoleCapabilityPermissionEffect.Allow;

    [MemoryPackOrder(5)]
    public virtual IdentityRole IdentityRole { get; set; } = null!;

    [MemoryPackIgnore]
    public string FeatureKey => TenantModuleFeatureKeys.Combine(ModuleKey, SubFeatureKey);
}
