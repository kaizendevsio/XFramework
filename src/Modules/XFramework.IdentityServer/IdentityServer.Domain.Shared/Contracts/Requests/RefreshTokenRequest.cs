namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = RefreshTokenRequest;
using TResponse = QueryResponse<RefreshTokenResponse>;

[MemoryPackable]
public partial record RefreshTokenRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public Guid SessionId { get; set; }
}
