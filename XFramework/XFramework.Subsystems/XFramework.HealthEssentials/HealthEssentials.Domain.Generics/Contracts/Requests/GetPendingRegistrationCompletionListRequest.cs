namespace HealthEssentials.Domain.Generics.Contracts.Requests;

using TRequest = GetPendingRegistrationCompletionListRequest;
using TResponse = QueryResponse<List<IdentityCredential>>;

public record GetPendingRegistrationCompletionListRequest : QueryableRequest, 
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>;
