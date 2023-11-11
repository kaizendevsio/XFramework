using IdentityServer.Domain.Generic.Contracts.Responses;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests;

public record CheckVerificationRequest : RequestBase, IRequest<QueryResponse<CheckVerificationResponse>>
{
    public Guid CredentialId { get; init; }
    public Guid VerificationTypeId { get; init; }
};
