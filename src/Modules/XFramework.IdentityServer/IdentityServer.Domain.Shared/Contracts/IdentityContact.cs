using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[AllowRemoteDataContextMutation]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/identity-contacts",
    RequireAuthorization = true,
    AuthorizationFeature = "identity.contacts"
)]
public partial class IdentityContact : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid? TypeId { get; set; }

    [MemoryPackOrder(1)]
    public string Value { get; set; } = null!;

    [MemoryPackOrder(2)]
    public Guid CredentialId { get; set; }


    [MemoryPackOrder(3)]
    public Guid GroupId { get; set; }

    [MemoryPackOrder(4)]
    public virtual IdentityContactType? Type { get; set; }

    [MemoryPackOrder(5)]
    public virtual IdentityContactGroup Group { get; set; } = null!;

    [MemoryPackOrder(6)]
    public virtual IdentityCredential Credential { get; set; } = null!;
}

public class CreateIdentityContactRequest
{
    public Guid? TypeId { get; set; }
    public string Value { get; set; } = null!;
    public Guid CredentialId { get; set; }
    public Guid GroupId { get; set; }
}

public class UpdateIdentityContactRequest
{
    public Guid? TypeId { get; set; }
    public string Value { get; set; } = null!;
    public Guid CredentialId { get; set; }
    public Guid GroupId { get; set; }
}

public class GetIdentityContactListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? TypeId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? CredentialId { get; set; }
}
