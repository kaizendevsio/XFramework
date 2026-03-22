using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/identity-address-types",
    RequireAuthorization = true,
    CacheDurationSeconds = 3600,
    CacheKeyPrefix = "identity-address-types"
)]
public partial class IdentityAddressType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }


    [MemoryPackOrder(1)]
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}

public class GetIdentityAddressTypeListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
}
