using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/tenants",
    RequireAuthorization = true,
    CacheDurationSeconds = 1800,
    CacheKeyPrefix = "tenants"
)]
public partial class Tenant : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }

    [MemoryPackOrder(1)]
    public string? Description { get; set; }


    [MemoryPackOrder(2)]
    public short? Status { get; set; }

    [MemoryPackOrder(3)]
    public DateTime? Expiration { get; set; }

    [MemoryPackOrder(4)]
    public DateTime? AvailabilityDate { get; set; }

    [MemoryPackOrder(5)]
    public Guid? ParentTenantId { get; set; }
    
    [MemoryPackOrder(6)]
    public decimal Version { get; set; }
    
    [MemoryPackOrder(7)]
    public virtual ICollection<IdentityCredential> IdentityCredentials { get; set; } = new List<IdentityCredential>();

    [MemoryPackOrder(8)]
    public virtual ICollection<IdentityInformation> IdentityInformations { get; set; } = new List<IdentityInformation>();

    [MemoryPackOrder(9)]
    public virtual ICollection<IdentityRoleType> IdentityRoleTypes { get; set; } = new List<IdentityRoleType>();

    [MemoryPackOrder(10)]
    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; set; } =
        new List<RegistryConfiguration>();

}

public class GetTenantListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public short? Status { get; set; }
}
