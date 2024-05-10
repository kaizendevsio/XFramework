using IdentityServer.Domain.Shared.Contracts.Responses;

namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = CheckVerificationRequest;
using TResponse = QueryResponse<CheckVerificationResponse>;

[MemoryPackable]
public partial record CheckVerificationRequest : RequestBase, 
    IRequest<TResponse>, 
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; init; }
    public Guid VerificationTypeId { get; init; }
};
