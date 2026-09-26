namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = AuthenticateIdentityRequest;
using TResponse = QueryResponse<AuthenticateIdentityResponse>;

[MemoryPackable]
public partial record AuthenticateIdentityRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid RoleId { get; set; }
    public AuthorizationType AuthorizationType { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool GenerateToken { get; set; } = true;
    public bool RememberMe { get; set; }
    /// <summary>
    /// A sign-in held by an installed app on the user's own device. The session gets no
    /// absolute expiry: it lasts until sign-out, revocation, a credential change, or the
    /// refresh token's sliding lifetime lapsing unused. Takes precedence over <see cref="RememberMe"/>.
    /// </summary>
    public bool PersistentSession { get; set; }
}
