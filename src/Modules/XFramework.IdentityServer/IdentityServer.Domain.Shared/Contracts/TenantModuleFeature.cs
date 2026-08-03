using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/tenant-module-features",
    RequireAuthorization = true
)]
public partial class TenantModuleFeature : BaseModel
{
    [MemoryPackOrder(0)]
    public string ModuleKey { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string SubFeatureKey { get; set; } = string.Empty;

    [MemoryPackOrder(2)]
    public string? DisplayName { get; set; }

    [MemoryPackOrder(3)]
    public string? Description { get; set; }

    [MemoryPackOrder(4)]
    public virtual Tenant Tenant { get; set; } = null!;

    [MemoryPackIgnore]
    public string Key => TenantModuleFeatureKeys.Combine(ModuleKey, SubFeatureKey);
}

public class CreateTenantModuleFeatureRequest
{
    public Guid TenantId { get; set; }
    public string ModuleKey { get; set; } = null!;
    public string SubFeatureKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class UpdateTenantModuleFeatureRequest
{
    public Guid TenantId { get; set; }
    public string ModuleKey { get; set; } = null!;
    public string SubFeatureKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class GetTenantModuleFeatureListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? TenantId { get; set; }
    public string? ModuleKey { get; set; }
    public string? SubFeatureKey { get; set; }
    public bool? IsEnabled { get; set; }
}
