using IdentityServer.Domain.Generic.Contracts.Responses;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests;

public record CheckVerificationRequest(
    Guid CredentialId,
    Guid VerificationTypeId
) : RequestBase, IRequest<QueryResponse<CheckVerificationResponse>>;
