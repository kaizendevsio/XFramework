using IdentityServer.Domain.Shared.Contracts.Responses;

namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = RefreshTokenRequest;
using TResponse = QueryResponse<RefreshTokenResponse>;

[MemoryPackable]
public partial record RefreshTokenRequest : RequestBase,
    IQuery<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public Guid SessionId { get; set; }
}
