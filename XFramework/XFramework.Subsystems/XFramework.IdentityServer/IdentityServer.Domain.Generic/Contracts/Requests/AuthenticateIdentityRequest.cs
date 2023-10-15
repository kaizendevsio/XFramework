using IdentityServer.Domain.Generic.Contracts.Responses;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests;

public record AuthenticateIdentityRequest(
    Guid RoleId,
    AuthorizationType AuthorizationType,
    string? Username,
    string? Password,
    bool GenerateToken = true
) : RequestBase, IRequest<QueryResponse<AuthenticateIdentityResponse>>;