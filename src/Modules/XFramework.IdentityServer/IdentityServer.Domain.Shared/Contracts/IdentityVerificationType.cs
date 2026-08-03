using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/identity-verification-types",
    RequireAuthorization = true
)]
public partial class IdentityVerificationType : BaseModel, IHasSystemReferenceId, IAllowsGlobalTenantRows
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }

    [MemoryPackOrder(1)]
    public long? DefaultExpiry { get; set; }

    [MemoryPackOrder(2)]
    public short? Priority { get; set; }


    [MemoryPackOrder(3)]
    public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; } = new List<IdentityVerification>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}

public class GetIdentityVerificationTypeListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
}
