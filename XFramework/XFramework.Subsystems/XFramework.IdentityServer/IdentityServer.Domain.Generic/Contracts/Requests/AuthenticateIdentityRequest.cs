using IdentityServer.Domain.Generic.Contracts.Responses;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests;

public record AuthenticateIdentityRequest : RequestBase, IRequest<QueryResponse<AuthenticateIdentityResponse>>
{
    public Guid RoleId { get; init; }
    public AuthorizationType AuthorizationType { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool GenerateToken { get; init; } = true;
}
