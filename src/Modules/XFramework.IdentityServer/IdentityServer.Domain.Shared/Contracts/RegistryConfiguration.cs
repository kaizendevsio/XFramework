using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/registry-configurations",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "registry-configurations"
)]
public partial class RegistryConfiguration : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Key { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string? Value { get; set; }

    [MemoryPackOrder(2)]
    public Guid GroupId { get; set; }

    [MemoryPackOrder(3)]
    public string? Unit { get; set; }

    [MemoryPackOrder(4)]
    public virtual Tenant Tenant { get; set; } = null!;

    [MemoryPackOrder(5)]
    public virtual RegistryConfigurationGroup? Group { get; set; }
}

public class CreateRegistryConfigurationRequest
{
    public string Key { get; set; } = null!;
    public string? Value { get; set; }
    public Guid GroupId { get; set; }
    public string? Unit { get; set; }
}

public class UpdateRegistryConfigurationRequest
{
    public string Key { get; set; } = null!;
    public string? Value { get; set; }
    public Guid GroupId { get; set; }
    public string? Unit { get; set; }
}

public class GetRegistryConfigurationListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? GroupId { get; set; }
}
