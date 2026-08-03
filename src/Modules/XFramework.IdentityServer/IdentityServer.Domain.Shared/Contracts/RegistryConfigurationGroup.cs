using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[AllowRemoteDataContextMutation]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/registry-configuration-groups",
    RequireAuthorization = true
)]
public partial class RegistryConfigurationGroup : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }

    [MemoryPackOrder(1)]
    public string? Description { get; set; }


    [MemoryPackOrder(2)]
    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; set; } =
        new List<RegistryConfiguration>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}

public class GetRegistryConfigurationGroupListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
}
