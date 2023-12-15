using IdentityServer.Domain.Generic.Contracts.Responses;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests;

public record AuthenticateIdentityRequest : RequestBase, IRequest<QueryResponse<AuthenticateIdentityResponse>>
{
    public Guid RoleId { get; set; }
    public AuthorizationType AuthorizationType { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool GenerateToken { get; set; } = true;
}
