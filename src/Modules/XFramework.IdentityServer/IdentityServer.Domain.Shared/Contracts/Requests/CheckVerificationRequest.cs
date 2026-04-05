namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = CheckVerificationRequest;
using TResponse = QueryResponse<CheckVerificationResponse>;

[MemoryPackable]
public partial record CheckVerificationRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; init; }
    public Guid VerificationTypeId { get; init; }
};
