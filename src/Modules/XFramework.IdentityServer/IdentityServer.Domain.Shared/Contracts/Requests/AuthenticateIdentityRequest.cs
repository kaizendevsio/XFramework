using IdentityServer.Domain.Shared.Contracts.Responses;

namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = AuthenticateIdentityRequest;
using TResponse = QueryResponse<AuthenticateIdentityResponse>;

public record AuthenticateIdentityRequest : RequestBase, 
    IRequest<TResponse>, 
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid RoleId { get; set; }
    public AuthorizationType AuthorizationType { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool GenerateToken { get; set; } = true;
}
