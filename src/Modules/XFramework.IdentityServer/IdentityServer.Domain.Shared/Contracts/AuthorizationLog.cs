using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/authorization-logs",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "authorization-logs"
)]
public partial class AuthorizationLog : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public string? Ipaddress { get; set; }

    [MemoryPackOrder(2)]
    public bool? IsSuccess { get; set; }

    [MemoryPackOrder(3)]
    public AuthenticationState? AuthStatus { get; set; }

    [MemoryPackOrder(4)]
    public string? LoginSource { get; set; }

    [MemoryPackOrder(5)]
    public string? DeviceName { get; set; }
    
    [MemoryPackOrder(6)]
    public string? DeviceAgent { get; set; }
    
    [MemoryPackOrder(7)]
    public Guid? SessionId { get; set; }
    
    [MemoryPackOrder(8)]
    public virtual IdentityCredential IdentityCredentials { get; set; } = null!;
}

public class GetAuthorizationLogListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public bool? IsSuccess { get; set; }
}
