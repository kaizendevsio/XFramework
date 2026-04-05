using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/sessions",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "sessions"
)]
public partial class Session : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid? SessionTypeId { get; set; }

    [MemoryPackOrder(1)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(2)]
    public string? SessionData { get; set; }

    [MemoryPackOrder(5)]
    public CurrentSessionState Status { get; set; } = CurrentSessionState.Active;

    [MemoryPackOrder(6)]
    public DateTime? ExpiresAt { get; set; }

    [MemoryPackOrder(3)]
    public virtual SessionType? SessionType { get; set; }

    [MemoryPackOrder(4)]
    public virtual IdentityCredential Credential { get; set; } = null!;
}

public class GetSessionListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public CurrentSessionState? Status { get; set; }
}
