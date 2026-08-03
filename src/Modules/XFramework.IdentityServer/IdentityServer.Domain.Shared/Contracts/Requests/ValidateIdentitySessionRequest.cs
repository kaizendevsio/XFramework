namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = ValidateIdentitySessionRequest;
using TResponse = QueryResponse<ValidateIdentitySessionResponse>;

[MemoryPackable]
public partial record ValidateIdentitySessionRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid SessionId { get; set; }
    public List<Guid> RoleTypeIds { get; set; } = [];
}
