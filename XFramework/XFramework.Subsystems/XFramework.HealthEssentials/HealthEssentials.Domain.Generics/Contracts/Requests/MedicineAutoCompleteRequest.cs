namespace HealthEssentials.Domain.Generics.Contracts.Requests;

using TRequest = MedicineAutoCompleteRequest;
using TResponse = QueryResponse<List<Medicine>>;

public record MedicineAutoCompleteRequest(string SearchString) : RequestBase,
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>;