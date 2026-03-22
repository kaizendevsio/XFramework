using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/identity-verifications",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "identity-verifications"
)]
public partial class IdentityVerification : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? VerificationTypeId { get; set; }

    [MemoryPackOrder(2)]
    public short? Status { get; set; }

    [MemoryPackOrder(3)]
    public DateTimeOffset? StatusUpdatedOn { get; set; }

    [MemoryPackOrder(4)]
    public string? Token { get; set; }

    [MemoryPackOrder(5)]
    public DateTime? Expiry { get; set; }


    [MemoryPackOrder(6)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(7)]
    public virtual IdentityVerificationType? VerificationType { get; set; }
}

public class GetIdentityVerificationListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public Guid? VerificationTypeId { get; set; }
}
