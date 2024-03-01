namespace HealthEssentials.Domain.Generics.Contracts.Requests;

using TRequest = ConcludeLiveConsultationRequest;
using TResponse = CmdResponse;

public record ConcludeLiveConsultationRequest(Guid Id) : RequestBase,
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>;