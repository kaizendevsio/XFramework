namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = ValidateIdentitySessionRequest;
using TResponse = QueryResponse<ValidateIdentitySessionResponse>;

[MemoryPackable]
public partial record ValidateIdentitySessionRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>;
