using XFramework.Domain.Shared.Attributes;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/identity-verifications",
    RequireAuthorization = true,
    AuthorizationFeature = "identity.verifications"
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

    [JsonIgnore]
    [MemoryPackIgnore]
    [NotMapped]
    public string? Token { get; set; }

    [JsonIgnore]
    [MemoryPackIgnore]
    public string? TokenHash { get; set; }

    [MemoryPackOrder(5)]
    public DateTime? Expiry { get; set; }

    [MemoryPackOrder(8)]
    public DateTimeOffset? ConsumedAt { get; set; }

    [MemoryPackOrder(9)]
    public string Purpose { get; set; } = IdentityConstants.VerificationPurpose.ContactVerification;

    [MemoryPackOrder(10)]
    public int FailedAttempts { get; set; }


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
